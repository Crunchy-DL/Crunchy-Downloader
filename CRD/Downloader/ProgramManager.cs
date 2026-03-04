using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Utils.Updater;
using FluentAvalonia.Styling;
using ProtoBuf.Meta;

namespace CRD.Downloader;

public partial class ProgramManager : ObservableObject{
    #region Singelton

    private static ProgramManager? _instance;
    private static readonly object Padlock = new();

    public static ProgramManager Instance{
        get{
            if (_instance == null){
                lock (Padlock){
                    if (_instance == null){
                        _instance = new ProgramManager();
                    }
                }
            }

            return _instance;
        }
    }

    #endregion


    #region Observables

    [ObservableProperty]
    private bool _fetchingData;

    [ObservableProperty]
    private bool _updateAvailable = true;

    [ObservableProperty]
    private double _opacityButton = 0.4;

    [ObservableProperty]
    private bool _finishedLoading;

    [ObservableProperty]
    private bool _navigationLock;

    #endregion


    public Dictionary<string, List<AnilistSeries>> AnilistSeasons = new();
    public Dictionary<string, List<CalendarEpisode>> AnilistUpcoming = new();

    private readonly FluentAvaloniaTheme? _faTheme;

    #region Startup Param Variables

    private Queue<Func<Task>> taskQueue = new Queue<Func<Task>>();
    bool historyRefreshAdded = false;
    private bool exitOnTaskFinish;

    #endregion

    private readonly PeriodicWorkRunner checkForNewEpisodesRunner;

    public IStorageProvider StorageProvider;

    public ProgramManager(){
        checkForNewEpisodesRunner = new PeriodicWorkRunner(async ct => { await CheckForDownloadsAsync(ct); });
        _faTheme = Application.Current?.Styles[0] as FluentAvaloniaTheme;

        foreach (var arg in Environment.GetCommandLineArgs()){
            switch (arg){
                case "--historyRefreshAll":
                    if (!historyRefreshAdded){
                        taskQueue.Enqueue(() => RefreshHistory(FilterType.All));
                        historyRefreshAdded = true;
                    }

                    break;
                case "--historyRefreshActive":
                    if (!historyRefreshAdded){
                        taskQueue.Enqueue(() => RefreshHistory(FilterType.Active));
                        historyRefreshAdded = true;
                    }

                    break;
                case "--historyAddToQueue":
                    taskQueue.Enqueue(AddMissingToQueue);
                    break;
                case "--exit":
                    exitOnTaskFinish = true;
                    break;
            }
        }

        _ = Init();

        CleanUpOldUpdater();
    }

    private async Task RefreshHistory(FilterType filterType){
        FetchingData = true;


        List<HistorySeries> filteredItems;
        var historyList = CrunchyrollManager.Instance.HistoryList;

        switch (filterType){
            case FilterType.All:
                filteredItems = historyList.ToList();
                break;

            case FilterType.MissingEpisodes:
                filteredItems = historyList.Where(item => item.NewEpisodes > 0).ToList();
                break;

            case FilterType.MissingEpisodesSonarr:
                filteredItems = historyList.Where(historySeries =>
                        !string.IsNullOrEmpty(historySeries.SonarrSeriesId) &&
                        historySeries.Seasons.Any(season =>
                            season.EpisodesList.Any(historyEpisode =>
                                !string.IsNullOrEmpty(historyEpisode.SonarrEpisodeId) && !historyEpisode.SonarrHasFile &&
                                (!CrunchyrollManager.Instance.CrunOptions.HistorySkipUnmonitored || historyEpisode.SonarrIsMonitored))))
                    .ToList();
                break;

            case FilterType.ContinuingOnly:
                filteredItems = historyList.Where(item => !string.IsNullOrEmpty(item.SonarrNextAirDate)).ToList();
                break;
            case FilterType.Active:
                filteredItems = historyList.Where(item => !item.IsInactive).ToList();
                break;
            case FilterType.Inactive:
                filteredItems = historyList.Where(item => item.IsInactive).ToList();
                break;

            default:
                filteredItems = new List<HistorySeries>();
                break;
        }

        foreach (var item in filteredItems){
            item.SetFetchingData();
        }

        for (int i = 0; i < filteredItems.Count; i++){
            await filteredItems[i].FetchData("");
            filteredItems[i].UpdateNewEpisodes();
        }

        FetchingData = false;
        CrunchyrollManager.Instance.History.SortItems();
    }

    private async Task AddMissingToQueue(){
        var tasks = CrunchyrollManager.Instance.HistoryList
            .Select(item => item.AddNewMissingToDownloads());

        await Task.WhenAll(tasks);


        while (QueueManager.Instance.Queue.Any(e => e.DownloadProgress is{ Done: false, Error: false })){
            Console.WriteLine("Waiting for downloads to complete...");
            await Task.Delay(2000);
        }
    }

    private async Task CheckForDownloadsAsync(CancellationToken ct){
        var crunchyManager = CrunchyrollManager.Instance;
        var crunOptions = crunchyManager.CrunOptions;

        if (!crunOptions.History){
            return;
        }

        switch (crunOptions.HistoryAutoRefreshMode){
            case HistoryRefreshMode.DefaultAll:
                await RefreshHistory(FilterType.All);
                break;
            case HistoryRefreshMode.DefaultActive:
                await RefreshHistory(FilterType.Active);
                break;
            case HistoryRefreshMode.FastNewReleases:
                var newEpisodesBase = await crunchyManager.CrEpisode.GetNewEpisodes(
                    string.IsNullOrEmpty(crunOptions.HistoryLang) ? crunchyManager.DefaultLocale : crunOptions.HistoryLang,
                    2000, null, true);
                if (newEpisodesBase is{ Data.Count: > 0 }){
                    var newEpisodes = newEpisodesBase.Data ?? [];

                    try{
                        await crunchyManager.History.UpdateWithEpisode(newEpisodes);
                        CfgManager.UpdateHistoryFile();
                    } catch (Exception e){
                        Console.Error.WriteLine("Failed to update History");
                    }
                }

                break;
            default:
                return;
        }

        var tasks = crunchyManager.HistoryList
            .Select(item => item.AddNewMissingToDownloads(true));

        await Task.WhenAll(tasks);
    }

    public void SetBackgroundImage(){
        if (!string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath)){
            Helpers.SetBackgroundImage(CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath, CrunchyrollManager.Instance.CrunOptions.BackgroundImageOpacity,
                CrunchyrollManager.Instance.CrunOptions.BackgroundImageBlurRadius);
        }
    }

    private async Task Init(){
        try{
            CrunchyrollManager.Instance.InitOptions();

            UpdateAvailable = await Updater.Instance.CheckForUpdatesAsync();

            OpacityButton = UpdateAvailable ? 1.0 : 0.4;

            if (CrunchyrollManager.Instance.CrunOptions.AccentColor != null && !string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.AccentColor)){
                if (_faTheme != null) _faTheme.CustomAccentColor = Color.Parse(CrunchyrollManager.Instance.CrunOptions.AccentColor);
            }

            if (_faTheme != null && Application.Current != null){
                if (CrunchyrollManager.Instance.CrunOptions.Theme == "System"){
                    _faTheme.PreferSystemTheme = true;
                } else if (CrunchyrollManager.Instance.CrunOptions.Theme == "Dark"){
                    _faTheme.PreferSystemTheme = false;
                    Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                } else{
                    _faTheme.PreferSystemTheme = false;
                    Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                }
            }

            await CrunchyrollManager.Instance.Init();

            FinishedLoading = true;

            await WorkOffArgsTasks();

            StartRunners(true);
        } catch (Exception e){
            Console.Error.WriteLine(e);
        } finally{
            NavigationLock = false;
        }
    }


    private async Task WorkOffArgsTasks(){
        if (taskQueue.Count == 0){
            return;
        }

        while (taskQueue.Count > 0){
            var task = taskQueue.Dequeue();
            await task(); // Execute the task asynchronously
        }

        Console.WriteLine("All tasks are completed.");

        if (exitOnTaskFinish){
            Console.WriteLine("Exiting...");
            IClassicDesktopStyleApplicationLifetime? lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current?.ApplicationLifetime;
            if (lifetime != null){
                lifetime.Shutdown();
            } else{
                Environment.Exit(0);
            }
        }
    }
    
    private void CleanUpOldUpdater(){
        var executableExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

        string backupFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"Updater{executableExtension}.bak");

        if (File.Exists(backupFilePath)){
            try{
                File.Delete(backupFilePath);
                Console.WriteLine($"Deleted old updater file: {backupFilePath}");
            } catch (Exception ex){
                Console.Error.WriteLine($"Failed to delete old updater file: {ex.Message}");
            }
        } else{
            Console.WriteLine("No old updater file found to delete.");
        }
    }
    
    public DateTime GetLastRefreshTime(){
        return checkForNewEpisodesRunner.LastRunTime;
    }
    
    public void StartRunners(bool runImmediately = false){
        checkForNewEpisodesRunner.StartOrRestartMinutes(CrunchyrollManager.Instance.CrunOptions.HistoryAutoRefreshIntervalMinutes,runImmediately);
    }
    
    public void StopBackgroundTasks(){
        checkForNewEpisodesRunner.Stop();
    }
    
}