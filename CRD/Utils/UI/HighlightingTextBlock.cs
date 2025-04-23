using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Structs.History;

namespace CRD.Utils.UI;

public class HighlightingTextBlock : TextBlock{
    public static readonly StyledProperty<IEnumerable<string>?> ItemsProperty =
        AvaloniaProperty.Register<HighlightingTextBlock, IEnumerable<string>?>(nameof(Items));

    public static readonly StyledProperty<HistorySeries?> SeriesProperty =
        AvaloniaProperty.Register<HighlightingTextBlock, HistorySeries?>(nameof(Series));

    public static readonly StyledProperty<HistorySeason?> SeasonProperty =
        AvaloniaProperty.Register<HighlightingTextBlock, HistorySeason?>(nameof(Season));

    public static readonly StyledProperty<StreamingService> StreamingServiceProperty =
        AvaloniaProperty.Register<HighlightingTextBlock, StreamingService>(nameof(StreamingService));

    public static readonly StyledProperty<bool> CheckDubsProperty =
        AvaloniaProperty.Register<HighlightingTextBlock, bool>(nameof(CheckDubs));

    public static readonly StyledProperty<bool> HighlightEntireTextProperty =
        AvaloniaProperty.Register<HighlightingTextBlock, bool>(nameof(HighlightEntireText));

    private HistorySeries? _lastSeries;
    private HistorySeason? _lastSeason;

    public bool HighlightEntireText{
        get => GetValue(HighlightEntireTextProperty);
        set => SetValue(HighlightEntireTextProperty, value);
    }

    public bool CheckDubs{
        get => GetValue(CheckDubsProperty);
        set => SetValue(CheckDubsProperty, value);
    }

    public IEnumerable<string>? Items{
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public HistorySeries? Series{
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public HistorySeason? Season{
        get => GetValue(SeasonProperty);
        set => SetValue(SeasonProperty, value);
    }

    public StreamingService StreamingService{
        get => GetValue(StreamingServiceProperty);
        set => SetValue(StreamingServiceProperty, value);
    }

    private void SubscribeSeries(HistorySeries series){
        series.HistorySeriesDubLangOverride.CollectionChanged += OnCollectionChanged;
        series.HistorySeriesSoftSubsOverride.CollectionChanged += OnCollectionChanged;
    }

    private void UnsubscribeSeries(HistorySeries series){
        series.HistorySeriesDubLangOverride.CollectionChanged -= OnCollectionChanged;
        series.HistorySeriesSoftSubsOverride.CollectionChanged -= OnCollectionChanged;
    }

    private void SubscribeSeason(HistorySeason season){
        season.HistorySeasonDubLangOverride.CollectionChanged += OnCollectionChanged;
        season.HistorySeasonSoftSubsOverride.CollectionChanged += OnCollectionChanged;
    }

    private void UnsubscribeSeason(HistorySeason season){
        season.HistorySeasonDubLangOverride.CollectionChanged -= OnCollectionChanged;
        season.HistorySeasonSoftSubsOverride.CollectionChanged -= OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e){
        UpdateText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e){
        base.OnDetachedFromVisualTree(e);

        if (_lastSeries != null)
            UnsubscribeSeries(_lastSeries);

        if (_lastSeason != null)
            UnsubscribeSeason(_lastSeason);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change){
        base.OnPropertyChanged(change);

        if (change.Property == SeriesProperty){
            if (_lastSeries != null)
                UnsubscribeSeries(_lastSeries);

            _lastSeries = change.NewValue as HistorySeries;

            if (_lastSeries != null)
                SubscribeSeries(_lastSeries);
        }

        if (change.Property == SeasonProperty){
            if (_lastSeason != null)
                UnsubscribeSeason(_lastSeason);

            _lastSeason = change.NewValue as HistorySeason;

            if (_lastSeason != null)
                SubscribeSeason(_lastSeason);
        }

        if (change.Property == ItemsProperty ||
            change.Property == SeriesProperty ||
            change.Property == SeasonProperty ||
            change.Property == StreamingServiceProperty ||
            change.Property == CheckDubsProperty){
            UpdateText();
        }
    }

    private void UpdateText(){
        Inlines?.Clear();
        if (Items == null) return;

        var streamingService = Series?.SeriesStreamingService ?? StreamingService;

        IEnumerable<string> source;

        if (CheckDubs){
            source =
                Season?.HistorySeasonDubLangOverride?.Any() == true ? Season.HistorySeasonDubLangOverride :
                Series?.HistorySeriesDubLangOverride?.Any() == true ? Series.HistorySeriesDubLangOverride :
                streamingService == StreamingService.Crunchyroll ? CrunchyrollManager.Instance.CrunOptions.DubLang :
                Enumerable.Empty<string>();
        } else{
            source =
                Season?.HistorySeasonSoftSubsOverride?.Any() == true ? Season.HistorySeasonSoftSubsOverride :
                Series?.HistorySeriesSoftSubsOverride?.Any() == true ? Series.HistorySeriesSoftSubsOverride :
                streamingService == StreamingService.Crunchyroll ? CrunchyrollManager.Instance.CrunOptions.DlSubs :
                Enumerable.Empty<string>();
        }

        var highlightSet = new HashSet<string>(source);


        foreach (var item in Items){
            var run = new Run(item);

            if (highlightSet.Contains(item)){
                run.Foreground = Brushes.Orange;
                // run.FontWeight = FontWeight.Bold;
            }

            Inlines?.Add(run);
            Inlines?.Add(new Run(", "));
        }

        if (Inlines?.Count > 0)
            Inlines.RemoveAt(Inlines.Count - 1);
    }
}