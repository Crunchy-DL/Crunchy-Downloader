using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Utils.UI;
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
    public static bool _showMonitoredBookmark;

    [ObservableProperty]
    public static bool _showFeaturedMusicButton;

    [ObservableProperty]
    public static bool _sonarrConnected;

    [ObservableProperty]
    private static EpisodeDownloadMode _selectedDownloadMode = EpisodeDownloadMode.OnlySubs;

    [ObservableProperty]
    public Symbol _selectedDownloadIcon = Symbol.ClosedCaption;

    private IStorageProvider? _storageProvider;

    public SeriesPageViewModel(){
        _storageProvider = ProgramManager.Instance.StorageProvider ?? throw new ArgumentNullException(nameof(ProgramManager.Instance.StorageProvider));

        _selectedSeries = CrunchyrollManager.Instance.SelectedSeries;

        if (_selectedSeries.ThumbnailImage == null){
            _ = _selectedSeries.LoadImage();
        }

        if (CrunchyrollManager.Instance.CrunOptions.SonarrProperties != null){
            SonarrConnected = CrunchyrollManager.Instance.CrunOptions.SonarrProperties.SonarrEnabled;

            if (!string.IsNullOrEmpty(SelectedSeries.SonarrSeriesId)){
                SonarrAvailable = SelectedSeries.SonarrSeriesId.Length > 0 && SonarrConnected;

                if (SonarrAvailable){
                    ShowMonitoredBookmark = CrunchyrollManager.Instance.CrunOptions.HistorySkipUnmonitored;
                }
            } else{
                SonarrAvailable = false;
            }
        } else{
            SonarrConnected = SonarrAvailable = false;
        }

        SelectedSeries.UpdateSeriesFolderPath();

        if (SelectedSeries.SeriesStreamingService == StreamingService.Crunchyroll && SelectedSeries.SeriesType != SeriesType.Artist){
            ShowFeaturedMusicButton = true;
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
            var folderPath = selectedFolder.Path.IsAbsoluteUri ? selectedFolder.Path.LocalPath : selectedFolder.Path.ToString();
            Console.WriteLine($"Selected folder: {folderPath}");

            if (season != null){
                season.SeasonDownloadPath = folderPath;
                CfgManager.UpdateHistoryFile();
            } else{
                SelectedSeries.SeriesDownloadPath = folderPath;
                CfgManager.UpdateHistoryFile();
            }
        }

        SelectedSeries.UpdateSeriesFolderPath();
    }

    [RelayCommand]
    public async Task OpenFeaturedMusicDialog(){
        if (SelectedSeries.SeriesStreamingService != StreamingService.Crunchyroll || SelectedSeries.SeriesType == SeriesType.Artist){
            return;
        }
        
        var musicList = await CrunchyrollManager.Instance.CrMusic.ParseFeaturedMusicVideoByIdAsync(SelectedSeries.SeriesId ?? string.Empty,
            CrunchyrollManager.Instance.CrunOptions.HistoryLang ?? CrunchyrollManager.Instance.DefaultLocale, true, true);

        if (musicList is{ Data.Count: > 0 }){
            var dialog = new CustomContentDialog(){
                Title = "Featured Music",
                CloseButtonText = "Close",
                FullSizeDesired = true
            };

            var viewModel = new ContentDialogFeaturedMusicViewModel(dialog, musicList, CrunchyrollManager.Instance.CrunOptions.HistoryIncludeCrArtists, SelectedSeries.SeriesFolderPathExists ? SelectedSeries.SeriesFolderPath : "");
            dialog.Content = new ContentDialogFeaturedMusicView(){
                DataContext = viewModel
            };

            var dialogResult = await dialog.ShowAsync();
        } else{
            MessageBus.Current.SendMessage(new ToastMessage($"No featured music found", ToastType.Warning, 3));
        }
    }

    [RelayCommand]
    public async Task MatchSonarrSeries_Button(){
        var dialog = new ContentDialog(){
            Title = "Sonarr Matching",
            PrimaryButtonText = "Save",
            CloseButtonText = "Close",
            FullSizeDesired = true
        };

        var viewModel = new ContentDialogSonarrMatchViewModel(dialog, SelectedSeries.SonarrSeriesId, SelectedSeries.SeriesTitle);
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

            _ = UpdateData("");
        }
    }

    [RelayCommand]
    public async Task MatchSonarrEpisode_Button(HistoryEpisode episode){
        var dialog = new CustomContentDialog(){
            Name = "CustomDialog",
            Title = "Sonarr Episode Matching",
            PrimaryButtonText = "Save",
            CloseButtonText = "Close",
            FullSizeDesired = true,
        };

        var viewModel = new ContentDialogSonarrMatchEpisodeViewModel(dialog, SelectedSeries, episode);
        dialog.Content = new ContentDialogSonarrMatchEpisodeView(){
            DataContext = viewModel
        };

        var dialogResult = await dialog.ShowAsync();

        if (dialogResult == ContentDialogResult.Primary){
            var sonarrEpisode = viewModel.CurrentSonarrEpisode;

            foreach (var selectedSeriesSeason in SelectedSeries.Seasons){
                foreach (var historyEpisode in selectedSeriesSeason.EpisodesList.Where(historyEpisode => historyEpisode.SonarrEpisodeId == sonarrEpisode.Id.ToString())){
                    historyEpisode.SonarrEpisodeId = string.Empty;
                    historyEpisode.SonarrAbsolutNumber = string.Empty;
                    historyEpisode.SonarrSeasonNumber = string.Empty;
                    historyEpisode.SonarrEpisodeNumber = string.Empty;
                    historyEpisode.SonarrHasFile = false;
                    historyEpisode.SonarrIsMonitored = false;
                }
            }

            episode.AssignSonarrEpisodeData(sonarrEpisode);
            CfgManager.UpdateHistoryFile();
        }
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
    public async Task DownloadEpisodeOnlyOptions(HistoryEpisode episode){
        var downloadMode = SelectedDownloadMode;

        if (downloadMode != EpisodeDownloadMode.Default){
            await episode.DownloadEpisode(downloadMode);
        }
    }

    [RelayCommand]
    public async Task DownloadSeasonAllOnlyOptions(HistorySeason season){
        var downloadMode = SelectedDownloadMode;

        if (downloadMode != EpisodeDownloadMode.Default){
            foreach (var episode in season.EpisodesList){
                await episode.DownloadEpisode(downloadMode);
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
    public void ToggleDownloadedMark(HistorySeason season){
        bool allDownloaded = season.EpisodesList.All(ep => ep.WasDownloaded);

        foreach (var historyEpisode in season.EpisodesList){
            if (historyEpisode.WasDownloaded == allDownloaded){
                season.UpdateDownloaded(historyEpisode.EpisodeId);
            }
        }
    }

    [RelayCommand]
    public async Task RefreshSonarrEpisodeMatch(){
        await CrunchyrollManager.Instance.History.MatchHistoryEpisodesWithSonarr(true, SelectedSeries);
        CfgManager.UpdateHistoryFile();
    }

    [RelayCommand]
    public async Task UpdateData(string? season){
        var result = await SelectedSeries.FetchData(season);

        MessageBus.Current.SendMessage(result
            ? new ToastMessage(string.IsNullOrEmpty(season) ? $"Series Refreshed" : $"Season Refreshed", ToastType.Information, 2)
            : new ToastMessage(string.IsNullOrEmpty(season) ? $"Series Refresh Failed" : $"Season Refresh Failed", ToastType.Error, 2));

        SelectedSeries.Seasons.Refresh();

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
    public void ToggleInactive(){
        CfgManager.UpdateHistoryFile();
    }

    [RelayCommand]
    public void NavBack(){
        SelectedSeries.UpdateNewEpisodes();
        MessageBus.Current.SendMessage(new NavigationMessage(null, true, false));
    }


    [RelayCommand]
    public void OpenFolderPath(){
        try{
            Process.Start(new ProcessStartInfo{
                FileName = SelectedSeries.SeriesFolderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred while opening the folder: {ex.Message}");
        }
    }
    
    [RelayCommand]
    public async Task OpenSeriesDetails(){
        CrSeriesBase? parsedSeries = await CrunchyrollManager.Instance.CrSeries.SeriesById(SelectedSeries.SeriesId ?? string.Empty, CrunchyrollManager.Instance.CrunOptions.HistoryLang, true);
        
        if (parsedSeries is{ Data.Length: > 0 }){
            var dialog = new CustomContentDialog(){
                Title = "Series",
                CloseButtonText = "Close",
                FullSizeDesired = true
            };

            var viewModel = new ContentDialogSeriesDetailsViewModel(dialog, parsedSeries,SelectedSeries.SeriesFolderPath);
            dialog.Content = new ContentDialogSeriesDetailsView(){
                DataContext = viewModel
            };

            var dialogResult = await dialog.ShowAsync();
        } else{
            MessageBus.Current.SendMessage(new ToastMessage($"Failed to get series details", ToastType.Warning, 3));
        }
        
            
    }


    partial void OnSelectedDownloadModeChanged(EpisodeDownloadMode value){
        SelectedDownloadIcon = SelectedDownloadMode switch{
            EpisodeDownloadMode.OnlyVideo => Symbol.Video,
            EpisodeDownloadMode.OnlyAudio => Symbol.Audio,
            EpisodeDownloadMode.OnlySubs => Symbol.ClosedCaption,
            _ => Symbol.ClosedCaption
        };
    }
}