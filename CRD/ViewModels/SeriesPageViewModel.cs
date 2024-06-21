using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Utils;
using CRD.Utils.Sonarr;
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

        if (!string.IsNullOrEmpty(SelectedSeries.SonarrSeriesId)){
            SonarrAvailable = SelectedSeries.SonarrSeriesId.Length > 0;
            Crunchyroll.Instance.CrHistory.MatchHistoryEpisodesWithSonarr(true,SelectedSeries);
            CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
        } else{
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
    public void OpenSonarrPage(){
        var sonarrProp = Crunchyroll.Instance.CrunOptions.SonarrProperties;

        if (sonarrProp == null) return;
        
        OpenUrl($"http{(sonarrProp.UseSsl ? "s" : "")}://{sonarrProp.Host}:{sonarrProp.Port}{(sonarrProp.UrlBase ?? "")}/series/{SelectedSeries.SonarrSlugTitle}");
    }

    [RelayCommand]
    public void OpenCrPage(){

        OpenUrl($"https://www.crunchyroll.com/series/{SelectedSeries.SeriesId}");
        
    }

    [RelayCommand]
    public async Task UpdateData(string? season){
        await SelectedSeries.FetchData(season);

        MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel), false, true));
    }

    [RelayCommand]
    public void RemoveSeason(string? season){
        HistorySeason? objectToRemove = SelectedSeries.Seasons.Find(se => se.SeasonId == season) ?? null;
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


    private void OpenUrl(string url){
        try{
            Process.Start(new ProcessStartInfo{
                FileName = url,
                UseShellExecute = true
            });
        } catch (Exception e){
            Console.Error.WriteLine($"An error occurred while trying to open URL - {url} : {e.Message}");
        }
    }
}