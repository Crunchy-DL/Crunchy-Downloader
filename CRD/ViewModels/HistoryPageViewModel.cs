using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Views;
using DynamicData;
using ReactiveUI;

namespace CRD.ViewModels;

public partial class HistoryPageViewModel : ViewModelBase{
    public ObservableCollection<HistorySeries> Items{ get; }

    [ObservableProperty]
    private static bool _fetchingData;

    [ObservableProperty]
    public HistorySeries _selectedSeries;

    [ObservableProperty]
    public static bool _editMode;

    [ObservableProperty]
    public double _scaleValue;

    [ObservableProperty]
    public ComboBoxItem _selectedView;

    public ObservableCollection<ComboBoxItem> ViewsList{ get; } =[];

    [ObservableProperty]
    public ComboBoxItem _selectedSorting;

    public ObservableCollection<ComboBoxItem> SortingList{ get; } =[];

    [ObservableProperty]
    public double _posterWidth;

    [ObservableProperty]
    public double _posterHeight;

    [ObservableProperty]
    public double _posterImageWidth;

    [ObservableProperty]
    public double _posterImageHeight;

    [ObservableProperty]
    public double _posterTextSize;

    [ObservableProperty]
    public Thickness _cornerMargin;

    private HistoryViewType currentViewType = HistoryViewType.Posters;

    [ObservableProperty]
    public bool _isPosterViewSelected = false;

    [ObservableProperty]
    public bool _isTableViewSelected = false;

    [ObservableProperty]
    public static bool _viewSelectionOpen;

    [ObservableProperty]
    public static bool _sortingSelectionOpen;

    private IStorageProvider _storageProvider;

    private SortingType currentSortingType = SortingType.NextAirDate;

    public HistoryPageViewModel(){
        Items = Crunchyroll.Instance.HistoryList;

        HistoryPageProperties? properties = Crunchyroll.Instance.CrunOptions.HistoryPageProperties;

        currentViewType = properties?.SelectedView ?? HistoryViewType.Posters;
        currentSortingType = properties?.SelectedSorting ?? SortingType.SeriesTitle;
        ScaleValue = properties?.ScaleValue ?? 0.73;

        foreach (HistoryViewType viewType in Enum.GetValues(typeof(HistoryViewType))){
            var combobox = new ComboBoxItem{ Content = viewType };
            ViewsList.Add(combobox);
            if (viewType == currentViewType){
                SelectedView = combobox;
            }
        }

        foreach (SortingType sortingType in Enum.GetValues(typeof(SortingType))){
            var combobox = new ComboBoxItem{ Content = sortingType.GetEnumMemberValue() };
            SortingList.Add(combobox);
            if (sortingType == currentSortingType){
                SelectedSorting = combobox;
            }
        }

        IsPosterViewSelected = currentViewType == HistoryViewType.Posters;
        IsTableViewSelected = currentViewType == HistoryViewType.Table;


        foreach (var historySeries in Items){
            if (historySeries.ThumbnailImage == null){
                historySeries.LoadImage();
            }

            historySeries.UpdateNewEpisodes();
        }

        Crunchyroll.Instance.CrHistory.SortItems();
    }


    private void UpdateSettings(){
        if (Crunchyroll.Instance.CrunOptions.HistoryPageProperties != null){
            Crunchyroll.Instance.CrunOptions.HistoryPageProperties.ScaleValue = ScaleValue;
            Crunchyroll.Instance.CrunOptions.HistoryPageProperties.SelectedView = currentViewType;
            Crunchyroll.Instance.CrunOptions.HistoryPageProperties.SelectedSorting = currentSortingType;
        } else{
            Crunchyroll.Instance.CrunOptions.HistoryPageProperties = new HistoryPageProperties(){ ScaleValue = ScaleValue, SelectedView = currentViewType, SelectedSorting = currentSortingType };
        }

        CfgManager.WriteSettingsToFile();
    }

    partial void OnSelectedViewChanged(ComboBoxItem value){
        if (Enum.TryParse(value.Content + "", out HistoryViewType viewType)){
            currentViewType = viewType;
            IsPosterViewSelected = currentViewType == HistoryViewType.Posters;
            IsTableViewSelected = currentViewType == HistoryViewType.Table;
        } else{
            Console.Error.WriteLine("Invalid viewtype selected");
        }

        ViewSelectionOpen = false;
        UpdateSettings();
    }

    partial void OnSelectedSortingChanged(ComboBoxItem value){
        if (TryParseEnum<SortingType>(value.Content + "", out var sortingType)){
            currentSortingType = sortingType;
            Crunchyroll.Instance.CrHistory.SortItems();
        } else{
            Console.Error.WriteLine("Invalid viewtype selected");
        }

        SortingSelectionOpen = false;
        UpdateSettings();
    }

    private bool TryParseEnum<T>(string value, out T result) where T : struct, Enum{
        foreach (var field in typeof(T).GetFields()){
            var attribute = field.GetCustomAttribute<EnumMemberAttribute>();
            if (attribute != null && attribute.Value == value){
                result = (T)field.GetValue(null);
                return true;
            }
        }

        result = default;
        return false;
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


    partial void OnSelectedSeriesChanged(HistorySeries value){
        Crunchyroll.Instance.SelectedSeries = value;

        if (!string.IsNullOrEmpty(value.SonarrSeriesId) && Crunchyroll.Instance.CrunOptions.SonarrProperties is{ SonarrEnabled: true }){
            Crunchyroll.Instance.CrHistory.MatchHistoryEpisodesWithSonarr(true, SelectedSeries);
            CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
        }

        NavToSeries();
        _selectedSeries = null;
    }

    [RelayCommand]
    public void RemoveSeries(string? seriesId){
        HistorySeries? objectToRemove = Crunchyroll.Instance.HistoryList.ToList().Find(se => se.SeriesId == seriesId) ?? null;
        if (objectToRemove != null){
            Crunchyroll.Instance.HistoryList.Remove(objectToRemove);
            Items.Remove(objectToRemove);
        }

        CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
    }


    [RelayCommand]
    public void NavToSeries(){
        if (FetchingData){
            return;
        }

        MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel), false, false));
    }

    [RelayCommand]
    public async void RefreshAll(){
        FetchingData = true;
        RaisePropertyChanged(nameof(FetchingData));
        for (int i = 0; i < Items.Count; i++){
            Items[i].SetFetchingData();
        }

        for (int i = 0; i < Items.Count; i++){
            FetchingData = true;
            RaisePropertyChanged(nameof(FetchingData));
            await Items[i].FetchData("");
            Items[i].UpdateNewEpisodes();
        }

        FetchingData = false;
        RaisePropertyChanged(nameof(FetchingData));
        Crunchyroll.Instance.CrHistory.SortItems();
    }

    [RelayCommand]
    public async void AddMissingToQueue(){
        for (int i = 0; i < Items.Count; i++){
            await Items[i].AddNewMissingToDownloads();
        }
    }

    [RelayCommand]
    public async Task OpenFolderDialogAsyncSeason(HistorySeason? season){
        if (_storageProvider == null){
            Console.Error.WriteLine("StorageProvider must be set before using the dialog.");
            throw new InvalidOperationException("StorageProvider must be set before using the dialog.");
        }


        var result = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions{
            Title = "Select Folder"
        });

        if (result.Count > 0){
            var selectedFolder = result[0];
            // Do something with the selected folder path
            Console.WriteLine($"Selected folder: {selectedFolder.Path.LocalPath}");

            if (season != null){
                season.SeasonDownloadPath = selectedFolder.Path.LocalPath;
            }

            CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
        }
    }

    [RelayCommand]
    public async Task OpenFolderDialogAsyncSeries(HistorySeries? series){
        if (_storageProvider == null){
            Console.Error.WriteLine("StorageProvider must be set before using the dialog.");
            throw new InvalidOperationException("StorageProvider must be set before using the dialog.");
        }


        var result = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions{
            Title = "Select Folder"
        });

        if (result.Count > 0){
            var selectedFolder = result[0];
            // Do something with the selected folder path
            Console.WriteLine($"Selected folder: {selectedFolder.Path.LocalPath}");

            if (series != null){
                series.SeriesDownloadPath = selectedFolder.Path.LocalPath;
            }

            CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
        }
    }


    public void SetStorageProvider(IStorageProvider storageProvider){
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
    }
}

public class HistoryPageProperties(){
    public SortingType? SelectedSorting{ get; set; }
    public HistoryViewType SelectedView{ get; set; }
    public double? ScaleValue{ get; set; }
}