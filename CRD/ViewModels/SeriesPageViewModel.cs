using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Utils;
using CRD.Utils.Sonarr;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Views;
using ReactiveUI;

namespace CRD.ViewModels;

public partial class SeriesPageViewModel : ViewModelBase{
    [ObservableProperty]
    public HistorySeries _selectedSeries;

    [ObservableProperty]
    public static bool _editMode;
    
    [ObservableProperty]
    public static bool _sonarrAvailable;

    private IStorageProvider _storageProvider;
    
    public SeriesPageViewModel(){
        _selectedSeries = Crunchyroll.Instance.SelectedSeries;

        if (_selectedSeries.ThumbnailImage == null){
            _selectedSeries.LoadImage();
        }
        
        if (!string.IsNullOrEmpty(SelectedSeries.SonarrSeriesId) && Crunchyroll.Instance.CrunOptions.SonarrProperties != null){
            SonarrAvailable = SelectedSeries.SonarrSeriesId.Length > 0 && Crunchyroll.Instance.CrunOptions.SonarrProperties.SonarrEnabled;
        }else{
            SonarrAvailable = false;
        }

        
    }
    
    [RelayCommand]
    public async Task OpenFolderDialogAsync(HistorySeason? season){
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
            } else{
                SelectedSeries.SeriesDownloadPath = selectedFolder.Path.LocalPath;
            }
            
            CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
        }
    }

    public void SetStorageProvider(IStorageProvider storageProvider){
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
    }
    
    
    [RelayCommand]
    public async Task UpdateData(string? season){
        await SelectedSeries.FetchData(season);
        
        SelectedSeries.Seasons.Refresh();
        
        // MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel), false, true));
    }

    [RelayCommand]
    public void RemoveSeason(string? season){
        HistorySeason? objectToRemove = SelectedSeries.Seasons.FirstOrDefault(se => se.SeasonId == season) ?? null;
        if (objectToRemove != null){
            SelectedSeries.Seasons.Remove(objectToRemove);
        }

        CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
        MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel), false, true));
    }


    [RelayCommand]
    public void NavBack(){
        SelectedSeries.UpdateNewEpisodes();
        MessageBus.Current.SendMessage(new NavigationMessage(null, true, false));
    }



}