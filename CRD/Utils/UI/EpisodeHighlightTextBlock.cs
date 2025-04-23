using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Structs.History;

namespace CRD.Utils.UI;

public class EpisodeHighlightTextBlock : TextBlock{
    public static readonly StyledProperty<HistorySeries?> SeriesProperty =
        AvaloniaProperty.Register<EpisodeHighlightTextBlock, HistorySeries?>(nameof(Series));

    public static readonly StyledProperty<HistorySeason?> SeasonProperty =
        AvaloniaProperty.Register<EpisodeHighlightTextBlock, HistorySeason?>(nameof(Season));

    public static readonly StyledProperty<HistoryEpisode?> EpisodeProperty =
        AvaloniaProperty.Register<EpisodeHighlightTextBlock, HistoryEpisode?>(nameof(Episode));

    public static readonly StyledProperty<StreamingService> StreamingServiceProperty =
        AvaloniaProperty.Register<EpisodeHighlightTextBlock, StreamingService>(nameof(StreamingService));


    private HistorySeries? _lastSeries;
    private HistorySeason? _lastSeason;

    public HistorySeries? Series{
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public HistorySeason? Season{
        get => GetValue(SeasonProperty);
        set => SetValue(SeasonProperty, value);
    }

    public HistoryEpisode? Episode{
        get => GetValue(EpisodeProperty);
        set => SetValue(EpisodeProperty, value);
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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e){
        base.OnDetachedFromVisualTree(e);

        if (_lastSeries != null)
            UnsubscribeSeries(_lastSeries);

        if (_lastSeason != null)
            UnsubscribeSeason(_lastSeason);
    }

    private void OnCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e){
        UpdateText();
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

        if (change.Property == SeriesProperty ||
            change.Property == SeasonProperty ||
            change.Property == StreamingServiceProperty){
            UpdateText();
        }
    }

    private void UpdateText(){

        Text = "E" + Episode?.Episode + " - " + Episode?.EpisodeTitle;
        
        var streamingService = Series?.SeriesStreamingService ?? StreamingService;
        
        var dubSet =
            Season?.HistorySeasonDubLangOverride.Any() == true ? new HashSet<string>(Season.HistorySeasonDubLangOverride) :
            Series?.HistorySeriesDubLangOverride.Any() == true ? new HashSet<string>(Series.HistorySeriesDubLangOverride) :
            streamingService == StreamingService.Crunchyroll ? new HashSet<string>(CrunchyrollManager.Instance.CrunOptions.DubLang) :
            new HashSet<string>();
        
        var subSet =
            Season?.HistorySeasonSoftSubsOverride.Any() == true ? new HashSet<string>(Season.HistorySeasonSoftSubsOverride) :
            Series?.HistorySeriesSoftSubsOverride.Any() == true ? new HashSet<string>(Series.HistorySeriesSoftSubsOverride) :
            streamingService == StreamingService.Crunchyroll ? new HashSet<string>(CrunchyrollManager.Instance.CrunOptions.DlSubs) :
            new HashSet<string>();
        
        var higlight = dubSet.IsSubsetOf(Episode?.HistoryEpisodeAvailableDubLang ?? []) &&
                       subSet.IsSubsetOf(Episode?.HistoryEpisodeAvailableSoftSubs ?? []);

        if (higlight){
            Foreground = Brushes.Orange;
        } else{
            ClearValue(ForegroundProperty);
        }
    }
}