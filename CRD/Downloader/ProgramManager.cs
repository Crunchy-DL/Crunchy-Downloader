using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Utils.Updater;
using FluentAvalonia.Styling;

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
    private bool _finishedLoading = false;

    #endregion


    public Dictionary<string, List<AnilistSeries>> AnilistSeasons = new();
    public Dictionary<string, List<CalendarEpisode>> AnilistUpcoming = new();

    private readonly FluentAvaloniaTheme? _faTheme;

    private Queue<Func<Task>> taskQueue = new Queue<Func<Task>>();

    private bool exitOnTaskFinish = false;

    public IStorageProvider StorageProvider;

    public ProgramManager(){
        _faTheme = Application.Current?.Styles[0] as FluentAvaloniaTheme;

        foreach (var arg in Environment.GetCommandLineArgs()){
            if (arg == "--historyRefreshAll"){
                taskQueue.Enqueue(RefreshAll);
            } else if (arg == "--historyAddToQueue"){
                taskQueue.Enqueue(AddMissingToQueue);
            } else if (arg == "--exit"){
                exitOnTaskFinish = true;
            }
        }

        Init();

        CleanUpOldUpdater();
    }

    private async Task RefreshAll(){
        FetchingData = true;

        foreach (var item in CrunchyrollManager.Instance.HistoryList){
            item.SetFetchingData();
        }

        for (int i = 0; i < CrunchyrollManager.Instance.HistoryList.Count; i++){
            await CrunchyrollManager.Instance.HistoryList[i].FetchData("");
            CrunchyrollManager.Instance.HistoryList[i].UpdateNewEpisodes();
        }

        FetchingData = false;
        CrunchyrollManager.Instance.History.SortItems();
    }

    private async Task AddMissingToQueue(){
        var tasks = CrunchyrollManager.Instance.HistoryList
            .Select(item => item.AddNewMissingToDownloads());

        await Task.WhenAll(tasks);


        while (QueueManager.Instance.Queue.Any(e => e.DownloadProgress != null && e.DownloadProgress.Done != true)){
            Console.WriteLine("Waiting for downloads to complete...");
            await Task.Delay(2000); // Wait for 2 second before checking again
        }
    }


    private async void Init(){
        CrunchyrollManager.Instance.InitOptions();

        UpdateAvailable = await Updater.Instance.CheckForUpdatesAsync();

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

        if (!string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath)){
            Helpers.SetBackgroundImage(CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath, CrunchyrollManager.Instance.CrunOptions.BackgroundImageOpacity,
                CrunchyrollManager.Instance.CrunOptions.BackgroundImageBlurRadius);
        }

        await CrunchyrollManager.Instance.Init();

        FinishedLoading = true;

        await WorkOffArgsTasks();
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
        string backupFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Updater.exe.bak");

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
}