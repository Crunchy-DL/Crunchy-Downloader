using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Sonarr;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Views;
using DynamicData;
using ReactiveUI;

namespace CRD.ViewModels;

public partial class HistoryPageViewModel : ViewModelBase{
    public ObservableCollection<HistorySeries> Items{ get; }
    public ObservableCollection<HistorySeries> FilteredItems{ get; }

    [ObservableProperty]
    private ProgramManager _programManager;

    [ObservableProperty]
    private HistorySeries? _selectedSeries;

    [ObservableProperty]
    private static bool _editMode;

    [ObservableProperty]
    private double _scaleValue;

    [ObservableProperty]
    private ComboBoxItem? _selectedView;

    public ObservableCollection<ComboBoxItem> ViewsList{ get; } =[];

    [ObservableProperty]
    private SortingListElement? _selectedSorting;

    public ObservableCollection<SortingListElement> SortingList{ get; } =[];

    [ObservableProperty]
    private FilterListElement? _selectedFilter;

    public ObservableCollection<FilterListElement> FilterList{ get; } =[];

    [ObservableProperty]
    private double _posterWidth;

    [ObservableProperty]
    private double _posterHeight;

    [ObservableProperty]
    private double _posterImageWidth;

    [ObservableProperty]
    private double _posterImageHeight;

    [ObservableProperty]
    private double _posterTextSize;

    [ObservableProperty]
    private Thickness _cornerMargin;


    [ObservableProperty]
    private bool _isPosterViewSelected;

    [ObservableProperty]
    private bool _isTableViewSelected;

    [ObservableProperty]
    private bool _showSeries = true;

    [ObservableProperty]
    private bool _showArtists;

    [ObservableProperty]
    private static bool _viewSelectionOpen;

    [ObservableProperty]
    private static bool _sortingSelectionOpen;

    [ObservableProperty]
    private static bool _addingMissingSonarrSeries;

    [ObservableProperty]
    private static bool _sonarrOptionsOpen;

    private IStorageProvider _storageProvider;

    private HistoryViewType currentViewType;

    private SortingType currentSortingType;

    private FilterType currentFilterType;

    [ObservableProperty]
    private static bool _sortDir;

    [ObservableProperty]
    private static bool _sonarrAvailable;

    [ObservableProperty]
    private static string _progressText;
    
    public Vector LastScrollOffset { get; set; } = Vector.Zero;

    public HistoryPageViewModel(){
        ProgramManager = ProgramManager.Instance;

        _storageProvider = ProgramManager.StorageProvider ?? throw new ArgumentNullException(nameof(ProgramManager.StorageProvider));

        if (CrunchyrollManager.Instance.CrunOptions.SonarrProperties != null){
            SonarrAvailable = CrunchyrollManager.Instance.CrunOptions.SonarrProperties.SonarrEnabled;
        } else{
            SonarrAvailable = false;
        }

        Items = CrunchyrollManager.Instance.HistoryList;
        FilteredItems = new ObservableCollection<HistorySeries>();

        HistoryPageProperties? properties = CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties;

        currentViewType = properties?.SelectedView ?? HistoryViewType.Posters;
        currentSortingType = properties?.SelectedSorting ?? SortingType.SeriesTitle;
        currentFilterType = properties?.SelectedFilter ?? FilterType.All;
        ScaleValue = properties?.ScaleValue ?? 0.73;
        SortDir = properties?.Ascending ?? false;
        ShowSeries = properties?.ShowSeries ?? true;
        ShowArtists = properties?.ShowArtists ?? false;

        foreach (HistoryViewType viewType in Enum.GetValues(typeof(HistoryViewType))){
            var combobox = new ComboBoxItem{ Content = viewType };
            ViewsList.Add(combobox);
            if (viewType == currentViewType){
                SelectedView = combobox;
            }
        }

        foreach (SortingType sortingType in Enum.GetValues(typeof(SortingType))){
            var combobox = new SortingListElement{ SortingTitle = sortingType.GetEnumMemberValue(), SelectedSorting = sortingType };
            SortingList.Add(combobox);
            if (sortingType == currentSortingType){
                SelectedSorting = combobox;
            }
        }

        foreach (FilterType filterType in Enum.GetValues(typeof(FilterType))){
            if (!SonarrAvailable && (filterType == FilterType.MissingEpisodesSonarr || filterType == FilterType.ContinuingOnly)){
                continue;
            }

            var item = new FilterListElement{ FilterTitle = filterType.GetEnumMemberValue(), SelectedType = filterType };
            FilterList.Add(item);
            if (filterType == currentFilterType){
                SelectedFilter = item;
            }
        }

        IsPosterViewSelected = currentViewType == HistoryViewType.Posters;
        IsTableViewSelected = currentViewType == HistoryViewType.Table;


        foreach (var historySeries in Items){
            if (historySeries.ThumbnailImage == null){
                _ = historySeries.LoadImage();
            }

            historySeries.UpdateNewEpisodes();
        }

        CrunchyrollManager.Instance.History.SortItems();
    }


    private void UpdateSettings(){
        if (CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties != null){
            CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.ScaleValue = ScaleValue;
            CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.SelectedView = currentViewType;
            CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.SelectedSorting = currentSortingType;
            CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.Ascending = SortDir;
        } else{
            CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties = new HistoryPageProperties
                { ScaleValue = ScaleValue, SelectedView = currentViewType, SelectedSorting = currentSortingType, Ascending = SortDir };
        }

        CfgManager.WriteCrSettings();
    }

    partial void OnSelectedViewChanged(ComboBoxItem? value){
        if (Enum.TryParse(value?.Content + "", out HistoryViewType viewType)){
            currentViewType = viewType;
            IsPosterViewSelected = currentViewType == HistoryViewType.Posters;
            IsTableViewSelected = currentViewType == HistoryViewType.Table;
        } else{
            Console.Error.WriteLine("Invalid viewtype selected");
        }

        ViewSelectionOpen = false;
        UpdateSettings();
    }


    partial void OnSelectedSortingChanged(SortingListElement? oldValue, SortingListElement? newValue){
        if (newValue == null){
            if (CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties != null){
                CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.Ascending = !CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.Ascending;
                SortDir = CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.Ascending;
            }

            Dispatcher.UIThread.InvokeAsync(() => {
                SelectedSorting = oldValue ?? SortingList.First();
                RaisePropertyChanged(nameof(SelectedSorting));
            });
            return;
        }

        currentSortingType = newValue.SelectedSorting;
        if (CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties != null) CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.SelectedSorting = currentSortingType;
        CrunchyrollManager.Instance.History.SortItems();
        if (SelectedFilter != null){
            OnSelectedFilterChanged(SelectedFilter);
        }

        SortingSelectionOpen = false;
        UpdateSettings();
    }

    partial void OnShowArtistsChanged(bool value){
        if (CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties != null) CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.ShowArtists = ShowArtists;

        CfgManager.WriteCrSettings();

        ApplyFilter();
    }

    partial void OnShowSeriesChanged(bool value){
        if (CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties != null) CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.ShowSeries = ShowSeries;

        CfgManager.WriteCrSettings();

        ApplyFilter();
    }


    partial void OnSelectedFilterChanged(FilterListElement? value){
        if (value == null){
            return;
        }

        currentFilterType = value.SelectedType;
        if (CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties != null) CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.SelectedFilter = currentFilterType;
        CfgManager.WriteCrSettings();


        ApplyFilter();
    }

    public void ApplyFilter(){
        List<HistorySeries> filteredItems;

        switch (currentFilterType){
            case FilterType.All:
                filteredItems = Items.ToList();
                break;

            case FilterType.MissingEpisodes:
                filteredItems = Items.Where(item => item.NewEpisodes > 0).ToList();
                break;

            case FilterType.MissingEpisodesSonarr:
                filteredItems = Items.Where(historySeries =>
                        !string.IsNullOrEmpty(historySeries.SonarrSeriesId) &&
                        historySeries.Seasons.Any(season =>
                            season.EpisodesList.Any(historyEpisode =>
                                !string.IsNullOrEmpty(historyEpisode.SonarrEpisodeId) && !historyEpisode.SonarrHasFile &&
                                (!CrunchyrollManager.Instance.CrunOptions.HistorySkipUnmonitored || historyEpisode.SonarrIsMonitored))))
                    .ToList();
                break;

            case FilterType.ContinuingOnly:
                filteredItems = Items.Where(item => !string.IsNullOrEmpty(item.SonarrNextAirDate)).ToList();
                break;
            case FilterType.Active:
                filteredItems = Items.Where(item => !item.IsInactive).ToList();
                break;
            case FilterType.Inactive:
                filteredItems = Items.Where(item => item.IsInactive).ToList();
                break;

            default:
                filteredItems = new List<HistorySeries>();
                break;
        }

        if (!ShowArtists){
            filteredItems.RemoveAll(item => item.SeriesType == SeriesType.Artist);
        }

        if (!ShowSeries){
            filteredItems.RemoveAll(item => item.SeriesType == SeriesType.Series);
        }

        FilteredItems.Clear();
        FilteredItems.AddRange(filteredItems);
    }


    partial void OnScaleValueChanged(double value){
        double t = (ScaleValue - 0.5) / (1 - 0.5);

        PosterHeight = Math.Clamp(225 + t * (410 - 225), 225, 410);
        PosterWidth = 250 * ScaleValue;
        PosterImageHeight = 360 * ScaleValue;
        PosterImageWidth = 240 * ScaleValue;


        double posterTextSizeCalc = 11 + t * (15 - 11);

        PosterTextSize = Math.Clamp(posterTextSizeCalc, 11, 15);
        CornerMargin = new Thickness(0, 0, Math.Clamp(3 + t * (5 - 3), 3, 5), 0);
        UpdateSettings();
    }


    partial void OnSelectedSeriesChanged(HistorySeries? value){
        if (value == null){
            return;
        }

        CrunchyrollManager.Instance.SelectedSeries = value;

        NavToSeries();

        if (!string.IsNullOrEmpty(value.SonarrSeriesId) && CrunchyrollManager.Instance.CrunOptions.SonarrProperties is{ SonarrEnabled: true }){
            if (SelectedSeries != null) _ = CrunchyrollManager.Instance.History.MatchHistoryEpisodesWithSonarr(false, SelectedSeries);
            CfgManager.UpdateHistoryFile();
        }

        SelectedSeries = null;
    }

    [RelayCommand]
    public void RemoveSeries(string? seriesId){
        HistorySeries? objectToRemove = CrunchyrollManager.Instance.HistoryList.ToList().Find(se => se.SeriesId == seriesId) ?? null;
        if (objectToRemove != null){
            CrunchyrollManager.Instance.HistoryList.Remove(objectToRemove);
            Items.Remove(objectToRemove);
            FilteredItems.Remove(objectToRemove);
            CfgManager.UpdateHistoryFile();
        }
    }


    [RelayCommand]
    public void NavToSeries(){
        if (ProgramManager.FetchingData && SelectedSeries is{ FetchingData: true }){
            return;
        }

        MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel), false, false));
    }

    [RelayCommand]
    public async Task RefreshAll(){
        ProgramManager.FetchingData = true;
        RaisePropertyChanged(nameof(ProgramManager.FetchingData));
        foreach (var item in FilteredItems){
            item.SetFetchingData();
        }

        for (int i = 0; i < FilteredItems.Count; i++){
            ProgramManager.FetchingData = true;
            RaisePropertyChanged(nameof(ProgramManager.FetchingData));
            await FilteredItems[i].FetchData("");
            FilteredItems[i].UpdateNewEpisodes();
        }

        ProgramManager.FetchingData = false;
        RaisePropertyChanged(nameof(ProgramManager.FetchingData));
        CrunchyrollManager.Instance.History.SortItems();
    }

    [RelayCommand]
    public async Task AddMissingToQueue(){
        var tasks = FilteredItems
            .Select(item => item.AddNewMissingToDownloads());

        await Task.WhenAll(tasks);
    }

    [RelayCommand]
    public async Task DownloadMissingSonarr(){
        await Task.WhenAll(
            FilteredItems.Where(series => !string.IsNullOrEmpty(series.SonarrSeriesId))
                .SelectMany(item => item.Seasons)
                .SelectMany(season => season.EpisodesList)
                .Where(historyEpisode => !string.IsNullOrEmpty(historyEpisode.SonarrEpisodeId) && !historyEpisode.SonarrHasFile)
                .Select(historyEpisode => historyEpisode.DownloadEpisode())
        );
    }

    [RelayCommand]
    public async Task AddMissingSonarrSeriesToHistory(){
        SonarrOptionsOpen = false;
        AddingMissingSonarrSeries = true;
        ProgramManager.FetchingData = true;
        ProgramManager.NavigationLock = true;

        var crInstance = CrunchyrollManager.Instance;

        if (crInstance.AllCRSeries == null){
            crInstance.AllCRSeries = await crInstance.CrSeries.GetAllSeries(string.IsNullOrEmpty(crInstance.CrunOptions.HistoryLang) ? crInstance.DefaultLocale : crInstance.CrunOptions.HistoryLang);
        }

        if (crInstance.AllCRSeries?.Data is{ Count: > 0 }){
            var concurrentSeriesIds = new ConcurrentBag<string>();

            Parallel.ForEach(SonarrClient.Instance.SonarrSeries, series => {
                if (crInstance.HistoryList.All(historySeries => historySeries.SonarrSeriesId != series.Id.ToString())){
                    var match = crInstance.History.FindClosestMatchCrSeries(crInstance.AllCRSeries.Data, series.Title);

                    if (match != null){
                        Console.WriteLine($"[Sonarr Match] Found match with {series.Title} and CR - {match.Title}");
                        if (!string.IsNullOrEmpty(match.Id)){
                            concurrentSeriesIds.Add(match.Id);
                        } else{
                            Console.Error.WriteLine($"[Sonarr Match] Series ID empty for {series.Title}");
                        }
                    } else{
                        Console.Error.WriteLine($"[Sonarr Match] Could not match {series.Title}");
                    }
                } else{
                    Console.Error.WriteLine($"[Sonarr Match] {series.Title} already matched");
                }
            });

            var seriesIds = concurrentSeriesIds.ToList();
            var totalSeries = seriesIds.Count;

            for (int count = 0; count < totalSeries; count++){
                ProgressText = $"{count + 1}/{totalSeries}";

                // Await the CRUpdateSeries task for each seriesId
                await crInstance.History.CrUpdateSeries(seriesIds[count], "");
                RaisePropertyChanged(nameof(ProgressText));
            }

            // var updateTasks = seriesIds.Select(seriesId => crInstance.History.CRUpdateSeries(seriesId, ""));
            // await Task.WhenAll(updateTasks);
        }

        ProgressText = "";
        AddingMissingSonarrSeries = false;
        ProgramManager.FetchingData = false;
        ProgramManager.NavigationLock = false;
        if (SelectedFilter != null){
            OnSelectedFilterChanged(SelectedFilter);
        }
    }


    [RelayCommand]
    public async Task OpenFolderDialogAsync(SeasonDialogArgs? seriesArgs){
        if (_storageProvider == null){
            Console.Error.WriteLine("StorageProvider must be set before using the dialog.");
            throw new InvalidOperationException("StorageProvider must be set before using the dialog.");
        }


        var result = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions{
            Title = "Select Folder"
        });

        if (result.Count > 0){
            var selectedFolder = result[0];
            var folderPath = selectedFolder.Path.IsAbsoluteUri ? selectedFolder.Path.LocalPath : selectedFolder.Path.ToString();
            Console.WriteLine($"Selected folder: {folderPath}");

            if (seriesArgs?.Season != null){
                seriesArgs.Season.SeasonDownloadPath = folderPath;
                CfgManager.UpdateHistoryFile();
            } else if (seriesArgs?.Series != null){
                seriesArgs.Series.SeriesDownloadPath = folderPath;
                CfgManager.UpdateHistoryFile();
            }
        }

        seriesArgs?.Series?.UpdateSeriesFolderPath();
    }

    [RelayCommand]
    public async Task DownloadSeasonAll(HistorySeason season){
        foreach (var episode in season.EpisodesList){
            await episode.DownloadEpisode();
        }
    }

    [RelayCommand]
    public async Task DownloadSeasonMissing(HistorySeason season){
        var missingEpisodes = season.EpisodesList
            .Where(episode => !episode.WasDownloaded).ToList();

        if (missingEpisodes.Count == 0){
            MessageBus.Current.SendMessage(new ToastMessage($"There are no missing episodes", ToastType.Error, 3));
        } else{
            foreach (var episode in missingEpisodes){
                await episode.DownloadEpisode();
            }
        }
    }

    [RelayCommand]
    public async Task DownloadSeasonMissingSonarr(HistorySeason season){
        foreach (var episode in season.EpisodesList.Where(episode => !episode.SonarrHasFile)){
            await episode.DownloadEpisode();
        }
    }

    [RelayCommand]
    public void ToggleDownloadedMark(SeasonDialogArgs seriesArgs){
        if (seriesArgs.Season != null){
            bool allDownloaded = seriesArgs.Season.EpisodesList.All(ep => ep.WasDownloaded);

            foreach (var historyEpisode in seriesArgs.Season.EpisodesList){
                if (historyEpisode.WasDownloaded == allDownloaded){
                    seriesArgs.Season.UpdateDownloaded(historyEpisode.EpisodeId);
                }
            }
        }

        seriesArgs.Series?.UpdateNewEpisodes();
    }

    [RelayCommand]
    public async Task UpdateData(SeasonDialogArgs seriesArgs){
        if (seriesArgs.Series != null){
            var result = await seriesArgs.Series.FetchData(seriesArgs.Season?.SeasonId);

            MessageBus.Current.SendMessage(result
                ? new ToastMessage(string.IsNullOrEmpty(seriesArgs.Season?.SeasonId) ? $"Series Refreshed" : $"Season Refreshed", ToastType.Information, 2)
                : new ToastMessage(string.IsNullOrEmpty(seriesArgs.Season?.SeasonId) ? $"Series Refresh Failed" : $"Season Refreshed Failed", ToastType.Error, 2));
        } else{
            MessageBus.Current.SendMessage(new ToastMessage(string.IsNullOrEmpty(seriesArgs.Season?.SeasonId) ? $"Refresh Failed" : $"Season Refresh Failed", ToastType.Error, 2));
            Console.Error.WriteLine("Failed to get Series Data from View Tree - report issue");
        }
    }

    [RelayCommand]
    public void OpenFolderPath(HistorySeries? series){
        try{
            Process.Start(new ProcessStartInfo{
                FileName = series?.SeriesFolderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred while opening the folder: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ToggleInactive(){
        CfgManager.UpdateHistoryFile();
    }
}

public class HistoryPageProperties{
    public SortingType? SelectedSorting{ get; set; }
    public HistoryViewType SelectedView{ get; set; }
    public FilterType SelectedFilter{ get; set; }
    public double? ScaleValue{ get; set; }

    public bool Ascending{ get; set; }

    public bool ShowSeries{ get; set; } = true;
    public bool ShowArtists{ get; set; } = true;
}

public class SeasonsPageProperties{
    public SortingType? SelectedSorting{ get; set; }

    public bool Ascending{ get; set; }
}

public class SortingListElement{
    public SortingType SelectedSorting{ get; set; }
    public string? SortingTitle{ get; set; }
}

public class FilterListElement{
    public FilterType SelectedType{ get; set; }
    public string? FilterTitle{ get; set; }
}