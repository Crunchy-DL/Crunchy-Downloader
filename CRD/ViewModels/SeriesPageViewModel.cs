using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Sonarr;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.ViewModels.Utils;
using CRD.Views;
using CRD.Views.Utils;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace CRD.ViewModels;

public partial class SeriesPageViewModel : ViewModelBase{
    [ObservableProperty]
    public HistorySeries _selectedSeries;

    [ObservableProperty]
    public static bool _editMode;

    [ObservableProperty]
    public static bool _sonarrAvailable;

    [ObservableProperty]
    public static bool _sonarrConnected;

    private IStorageProvider? _storageProvider;

    [ObservableProperty]
    private string _availableDubs;

    [ObservableProperty]
    private string _availableSubs;

    public SeriesPageViewModel(){
        _selectedSeries = CrunchyrollManager.Instance.SelectedSeries;

        if (_selectedSeries.ThumbnailImage == null){
            _selectedSeries.LoadImage();
        }

        if (CrunchyrollManager.Instance.CrunOptions.SonarrProperties != null){
            SonarrConnected = CrunchyrollManager.Instance.CrunOptions.SonarrProperties.SonarrEnabled;

            if (!string.IsNullOrEmpty(SelectedSeries.SonarrSeriesId)){
                SonarrAvailable = SelectedSeries.SonarrSeriesId.Length > 0 && SonarrConnected;
            } else{
                SonarrAvailable = false;
            }
        } else{
            SonarrConnected = SonarrAvailable = false;
        }

        AvailableDubs = "Available Dubs: " + string.Join(", ", SelectedSeries.HistorySeriesAvailableDubLang);
        AvailableSubs = "Available Subs: " + string.Join(", ", SelectedSeries.HistorySeriesAvailableSoftSubs);
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
                CfgManager.UpdateHistoryFile();
            } else{
                SelectedSeries.SeriesDownloadPath = selectedFolder.Path.LocalPath;
                CfgManager.UpdateHistoryFile();
            }
        }
    }

    public void SetStorageProvider(IStorageProvider storageProvider){
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
    }

    [RelayCommand]
    public async Task MatchSonarrSeries_Button(){
        var dialog = new ContentDialog(){
            Title = "Sonarr Matching",
            PrimaryButtonText = "Save",
            CloseButtonText = "Close",
            FullSizeDesired = true
        };

        var viewModel = new ContentDialogSonarrMatchViewModel(dialog, SelectedSeries.SonarrSeriesId,SelectedSeries.SeriesTitle);
        dialog.Content = new ContentDialogSonarrMatchView(){
            DataContext = viewModel
        };

        var dialogResult = await dialog.ShowAsync();

        if (dialogResult == ContentDialogResult.Primary){
            SelectedSeries.SonarrSeriesId = viewModel.CurrentSonarrSeries.Id.ToString();
            SelectedSeries.SonarrTvDbId = viewModel.CurrentSonarrSeries.TvdbId.ToString();
            SelectedSeries.SonarrSlugTitle = viewModel.CurrentSonarrSeries.TitleSlug;
            
            if (CrunchyrollManager.Instance.CrunOptions.SonarrProperties != null){
                SonarrConnected = CrunchyrollManager.Instance.CrunOptions.SonarrProperties.SonarrEnabled;
                
                if (!string.IsNullOrEmpty(SelectedSeries.SonarrSeriesId)){
                    SonarrAvailable = SelectedSeries.SonarrSeriesId.Length > 0 && SonarrConnected;
                } else{
                    SonarrAvailable = false;
                }
            } else{
                SonarrConnected = SonarrAvailable = false;
            }

            UpdateData("");
        }
        
    }


    [RelayCommand]
    public async Task DownloadSeasonAll(HistorySeason season){
        var downloadTasks = season.EpisodesList
            .Select(episode => episode.DownloadEpisode());

        await Task.WhenAll(downloadTasks);
    }

    [RelayCommand]
    public async Task DownloadSeasonMissing(HistorySeason season){
        var downloadTasks = season.EpisodesList
            .Where(episode => !episode.WasDownloaded)
            .Select(episode => episode.DownloadEpisode());

        await Task.WhenAll(downloadTasks);
    }

    [RelayCommand]
    public async Task DownloadSeasonMissingSonarr(HistorySeason season){
        var downloadTasks = season.EpisodesList
            .Where(episode => !episode.SonarrHasFile)
            .Select(episode => episode.DownloadEpisode());

        await Task.WhenAll(downloadTasks);
    }

    [RelayCommand]
    public async Task UpdateData(string? season){
        await SelectedSeries.FetchData(season);

        SelectedSeries.Seasons.Refresh();

        AvailableDubs = "Available Dubs: " + string.Join(", ", SelectedSeries.HistorySeriesAvailableDubLang);
        AvailableSubs = "Available Subs: " + string.Join(", ", SelectedSeries.HistorySeriesAvailableSoftSubs);

        // MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel), false, true));
    }

    [RelayCommand]
    public void RemoveSeason(string? season){
        HistorySeason? objectToRemove = SelectedSeries.Seasons.FirstOrDefault(se => se.SeasonId == season) ?? null;
        if (objectToRemove != null){
            SelectedSeries.Seasons.Remove(objectToRemove);
            CfgManager.UpdateHistoryFile();
        }

        MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel), false, true));
    }


    [RelayCommand]
    public void NavBack(){
        SelectedSeries.UpdateNewEpisodes();
        MessageBus.Current.SendMessage(new NavigationMessage(null, true, false));
    }
}