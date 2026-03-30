using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.CustomList;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.ViewModels;
using CRD.Views;
using ReactiveUI;

namespace CRD.Downloader;

public sealed partial class QueueManager : ObservableObject{
    
    public static QueueManager Instance{ get; } = new();
    
     #region Download Variables

    public RefreshableObservableCollection<CrunchyEpMeta> Queue{ get; } = new();
    public ObservableCollection<DownloadItemModel> DownloadItemModels{ get; } = new();

    public int ActiveDownloads{
        get{
            lock (downloadStartLock){
                return activeOrStarting.Count;
            }
        }
    }

    private readonly object downloadStartLock = new();
    private readonly HashSet<CrunchyEpMeta> activeOrStarting = new();
    private readonly object processingLock = new();

    private readonly SemaphoreSlim activeProcessingJobs;
    private int processingJobsLimit;
    private int borrowed;

    private int pumpScheduled;
    private int pumpDirty;

    #endregion

    [ObservableProperty]
    private bool hasFailedItem;

    public event EventHandler? QueueStateChanged;

    private readonly CrunchyrollManager crunchyrollManager;

    public QueueManager(){
        this.crunchyrollManager = CrunchyrollManager.Instance;

        activeProcessingJobs = new SemaphoreSlim(
            initialCount: crunchyrollManager.CrunOptions.SimultaneousProcessingJobs,
            maxCount: 2);

        processingJobsLimit = crunchyrollManager.CrunOptions.SimultaneousProcessingJobs;

        Queue.CollectionChanged += UpdateItemListOnRemove;
        Queue.CollectionChanged += (_, _) => OnQueueStateChanged();
    }

    public bool TryStartDownload(DownloadItemModel model){
        var item = model.epMeta;

        lock (downloadStartLock){
            if (activeOrStarting.Contains(item))
                return false;

            if (item.DownloadProgress is{ IsDownloading: true })
                return false;

            if (item.DownloadProgress is{ Done: true })
                return false;

            if (item.DownloadProgress is{ Error: true })
                return false;

            if (activeOrStarting.Count >= crunchyrollManager.CrunOptions.SimultaneousDownloads)
                return false;

            activeOrStarting.Add(item);
        }

        OnQueueStateChanged();
        _ = model.StartDownloadCore();
        return true;
    }

    public void ReleaseDownloadSlot(CrunchyEpMeta item){
        bool removed;

        lock (downloadStartLock){
            removed = activeOrStarting.Remove(item);
        }

        if (removed){
            OnQueueStateChanged();
            RequestPump();
        }
    }

    public Task WaitForProcessingSlotAsync(CancellationToken cancellationToken = default){
        return activeProcessingJobs.WaitAsync(cancellationToken);
    }

    public void ReleaseProcessingSlot(){
        lock (processingLock){
            if (borrowed > 0){
                borrowed--;
                return;
            }

            activeProcessingJobs.Release();
        }
    }

    public void SetLimit(int newLimit){
        if (newLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(newLimit));

        lock (processingLock){
            if (newLimit == processingJobsLimit)
                return;

            int delta = newLimit - processingJobsLimit;

            if (delta > 0){
                int giveBack = Math.Min(borrowed, delta);
                borrowed -= giveBack;

                int toRelease = delta - giveBack;
                if (toRelease > 0)
                    activeProcessingJobs.Release(toRelease);
            } else{
                int toRemove = -delta;


                while (toRemove > 0 && activeProcessingJobs.Wait(0)){
                    toRemove--;
                }


                borrowed += toRemove;
            }

            processingJobsLimit = newLimit;
        }
    }

    private void UpdateItemListOnRemove(object? sender, NotifyCollectionChangedEventArgs e){
        if (e.Action == NotifyCollectionChangedAction.Remove){
            if (e.OldItems != null)
                foreach (var eOldItem in e.OldItems){
                    var downloadItem = DownloadItemModels.FirstOrDefault(downloadItem => downloadItem.epMeta.Equals(eOldItem));
                    if (downloadItem != null){
                        DownloadItemModels.Remove(downloadItem);
                    } else{
                        Console.Error.WriteLine("Failed to Remove Episode from list");
                    }
                }
        } else if (e.Action == NotifyCollectionChangedAction.Reset && Queue.Count == 0){
            DownloadItemModels.Clear();
        }

        UpdateDownloadListItems();
    }

    public void MarkDownloadFinished(CrunchyEpMeta item, bool removeFromQueue){
        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            if (removeFromQueue){
                if (Queue.Contains(item))
                    Queue.Remove(item);
            } else{
                Queue.Refresh();
            }

            OnQueueStateChanged();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    public void UpdateDownloadListItems(){
        foreach (CrunchyEpMeta crunchyEpMeta in Queue.ToList()){
            var downloadItem = DownloadItemModels.FirstOrDefault(e => e.epMeta.Equals(crunchyEpMeta));
            if (downloadItem != null){
                downloadItem.Refresh();
            } else{
                downloadItem = new DownloadItemModel(crunchyEpMeta);
                _ = downloadItem.LoadImage();
                DownloadItemModels.Add(downloadItem);
            }
        }

        HasFailedItem = Queue.Any(item => item.DownloadProgress.Error);

        if (crunchyrollManager.CrunOptions.AutoDownload){
            RequestPump();
        }
    }

    public void RequestPump(){
        Interlocked.Exchange(ref pumpDirty, 1);

        if (Interlocked.CompareExchange(ref pumpScheduled, 1, 0) != 0)
            return;

        Avalonia.Threading.Dispatcher.UIThread.Post(
            RunPump,
            Avalonia.Threading.DispatcherPriority.Background);
    }

    private void RunPump(){
        try{
            while (Interlocked.Exchange(ref pumpDirty, 0) == 1){
                PumpQueue();
            }
        } finally{
            Interlocked.Exchange(ref pumpScheduled, 0);
            
            if (Volatile.Read(ref pumpDirty) == 1 &&
                Interlocked.CompareExchange(ref pumpScheduled, 1, 0) == 0){
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    RunPump,
                    Avalonia.Threading.DispatcherPriority.Background);
            }
        }
    }

    private void PumpQueue(){
        List<CrunchyEpMeta> toStart = new();

        lock (downloadStartLock){
            int limit = crunchyrollManager.CrunOptions.SimultaneousDownloads;
            int freeSlots = Math.Max(0, limit - activeOrStarting.Count);

            if (freeSlots == 0)
                return;

            foreach (var item in Queue.ToList()){
                if (freeSlots == 0)
                    break;

                if (item.DownloadProgress.Error)
                    continue;

                if (item.DownloadProgress.Done)
                    continue;

                if (item.DownloadProgress.IsDownloading)
                    continue;

                if (activeOrStarting.Contains(item))
                    continue;

                activeOrStarting.Add(item);
                freeSlots--;
                toStart.Add(item);
            }
        }

        foreach (var item in toStart){
            var model = DownloadItemModels.FirstOrDefault(x => x.epMeta.Equals(item));
            if (model != null){
                _ = model.StartDownloadCore();
            } else{
                ReleaseDownloadSlot(item);
            }
        }

        OnQueueStateChanged();
    }


    private void OnQueueStateChanged(){
        QueueStateChanged?.Invoke(this, EventArgs.Empty);
    }


    public async Task CrAddEpisodeToQueue(string epId, string crLocale, List<string> dubLang, bool updateHistory = false, EpisodeDownloadMode episodeDownloadMode = EpisodeDownloadMode.Default){
        if (string.IsNullOrEmpty(epId)){
            return;
        }

        await CrunchyrollManager.Instance.CrAuthEndpoint1.RefreshToken(true);

        var episodeL = await CrunchyrollManager.Instance.CrEpisode.ParseEpisodeById(epId, crLocale);


        if (episodeL != null){
            if (episodeL.IsPremiumOnly && !CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.HasPremium){
                MessageBus.Current.SendMessage(new ToastMessage($"Episode is a premium episode – make sure that you are signed in with an account that has an active premium subscription", ToastType.Error, 3));
                return;
            }

            var sList = await CrunchyrollManager.Instance.CrEpisode.EpisodeData(episodeL, updateHistory);

            (HistoryEpisode? historyEpisode, List<string> dublist, List<string> sublist, string downloadDirPath, string videoQuality) historyEpisode = (null, [], [], "", "");

            if (CrunchyrollManager.Instance.CrunOptions.History){
                var variant = sList.EpisodeAndLanguages.Variants.First();
                historyEpisode = CrunchyrollManager.Instance.History.GetHistoryEpisodeWithDubListAndDownloadDir(variant.Item.SeriesId, variant.Item.SeasonId, variant.Item.Id);
                if (historyEpisode.dublist.Count > 0){
                    dubLang = historyEpisode.dublist;
                }
            }


            var selected = CrunchyrollManager.Instance.CrEpisode.EpisodeMeta(sList, dubLang);

            if (CrunchyrollManager.Instance.CrunOptions.IncludeVideoDescription){
                if (selected.Data is{ Count: > 0 }){
                    var episode = await CrunchyrollManager.Instance.CrEpisode.ParseEpisodeById(selected.Data.First().MediaId,
                        string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.DescriptionLang) ? CrunchyrollManager.Instance.DefaultLocale : CrunchyrollManager.Instance.CrunOptions.DescriptionLang, true);
                    selected.Description = episode?.Description ?? selected.Description;
                }
            }

            if (selected.Data is{ Count: > 0 }){
                if (CrunchyrollManager.Instance.CrunOptions.History){
                    // var historyEpisode = CrHistory.GetHistoryEpisodeWithDownloadDir(selected.ShowId, selected.SeasonId, selected.Data.First().MediaId);
                    if (CrunchyrollManager.Instance.CrunOptions.SonarrProperties is{ SonarrEnabled: true, UseSonarrNumbering: true }){
                        if (historyEpisode.historyEpisode != null){
                            if (!string.IsNullOrEmpty(historyEpisode.historyEpisode.SonarrEpisodeNumber)){
                                selected.EpisodeNumber = historyEpisode.historyEpisode.SonarrEpisodeNumber;
                            }

                            if (!string.IsNullOrEmpty(historyEpisode.historyEpisode.SonarrSeasonNumber)){
                                selected.Season = historyEpisode.historyEpisode.SonarrSeasonNumber;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(historyEpisode.downloadDirPath)){
                        selected.DownloadPath = historyEpisode.downloadDirPath;
                    }
                }

                selected.VideoQuality = !string.IsNullOrEmpty(historyEpisode.videoQuality) ? historyEpisode.videoQuality : CrunchyrollManager.Instance.CrunOptions.QualityVideo;

                selected.DownloadSubs = historyEpisode.sublist.Count > 0 ? historyEpisode.sublist : CrunchyrollManager.Instance.CrunOptions.DlSubs;

                selected.OnlySubs = episodeDownloadMode == EpisodeDownloadMode.OnlySubs;

                if (CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub && selected.Data.Count > 1){
                    var sortedMetaData = selected.Data
                        .OrderBy(metaData => {
                            var locale = metaData.Lang?.CrLocale ?? string.Empty;
                            var index = dubLang.IndexOf(locale);
                            return index != -1 ? index : int.MaxValue;
                        })
                        .ToList();

                    if (sortedMetaData.Count != 0){
                        var first = sortedMetaData.First();
                        selected.Data = [first];
                        selected.SelectedDubs = [first.Lang?.CrLocale ?? string.Empty];
                    }
                }

                var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);

                if (newOptions == null){
                    Console.Error.WriteLine("Failed to create a copy of your current settings");
                    MessageBus.Current.SendMessage(new ToastMessage($"Couldn't add episode to the queue, check the logs", ToastType.Error, 2));
                    return;
                }

                switch (episodeDownloadMode){
                    case EpisodeDownloadMode.OnlyVideo:
                        newOptions.Novids = false;
                        newOptions.Noaudio = true;
                        selected.DownloadSubs = ["none"];
                        break;
                    case EpisodeDownloadMode.OnlyAudio:
                        newOptions.Novids = true;
                        newOptions.Noaudio = false;
                        selected.DownloadSubs = ["none"];
                        break;
                    case EpisodeDownloadMode.OnlySubs:
                        newOptions.Novids = true;
                        newOptions.Noaudio = true;
                        break;
                    case EpisodeDownloadMode.Default:
                    default:
                        break;
                }

                if (!selected.DownloadSubs.Contains("none") && selected.DownloadSubs.All(item => (selected.AvailableSubs ?? []).Contains(item))){
                    if (!(selected.Data.Count < dubLang.Count && !CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub)){
                        selected.HighlightAllAvailable = true;
                    }
                }

                if (newOptions.DownloadOnlyWithAllSelectedDubSub){
                    if (!selected.DownloadSubs.Contains("none") && !selected.DownloadSubs.Contains("all") && !selected.DownloadSubs.All(item => (selected.AvailableSubs ?? []).Contains(item))){
                        //missing subs
                        Console.Error.WriteLine($"Episode not added because of missing subs - {selected.SeasonTitle} - Season {selected.Season} - {selected.EpisodeTitle}");
                        return;
                    }

                    if (selected.Data.Count < dubLang.Count && !CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub){
                        //missing dubs
                        Console.Error.WriteLine($"Episode not added because of missing dubs - {selected.SeasonTitle} - Season {selected.Season} - {selected.EpisodeTitle}");
                        return;
                    }
                }

                newOptions.DubLang = dubLang;

                selected.DownloadSettings = newOptions;

                Queue.Add(selected);


                if (selected.Data.Count < dubLang.Count && !CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub){
                    Console.WriteLine("Added Episode to Queue but couldn't find all selected dubs");
                    Console.Error.WriteLine("Added Episode to Queue but couldn't find all selected dubs - Available dubs/subs: ");

                    var languages = sList.EpisodeAndLanguages.Variants
                        .Select(v => $"{(v.Item.IsPremiumOnly ? "+ " : "")}{v.Lang.CrLocale}")
                        .ToArray();

                    Console.Error.WriteLine(
                        $"{selected.SeasonTitle} - Season {selected.Season} - {selected.EpisodeTitle} dubs - [{string.Join(", ", languages)}] subs - [{string.Join(", ", selected.AvailableSubs ?? [])}]");
                    MessageBus.Current.SendMessage(new ToastMessage($"Added episode to the queue but couldn't find all selected dubs", ToastType.Warning, 2));
                } else{
                    Console.WriteLine("Added Episode to Queue");
                    MessageBus.Current.SendMessage(new ToastMessage($"Added episode to the queue", ToastType.Information, 1));
                }
            } else{
                Console.WriteLine("Episode couldn't be added to Queue");
                Console.Error.WriteLine("Episode couldn't be added to Queue - Available dubs/subs: ");

                var languages = sList.EpisodeAndLanguages.Variants
                    .Select(v => $"{(v.Item.IsPremiumOnly ? "+ " : "")}{v.Lang.CrLocale}")
                    .ToArray();

                Console.Error.WriteLine($"{selected.SeasonTitle} - Season {selected.Season} - {selected.EpisodeTitle} dubs - [{string.Join(", ", languages)}] subs - [{string.Join(", ", selected.AvailableSubs ?? [])}]");
                if (!CrunchyrollManager.Instance.CrunOptions.DownloadOnlyWithAllSelectedDubSub){
                    MessageBus.Current.SendMessage(new ToastMessage($"Couldn't add episode to the queue with current dub settings", ToastType.Error, 2));
                }
            }

            return;
        }

        Console.WriteLine("Couldn't find episode trying to find movie with id");

        var movie = await CrunchyrollManager.Instance.CrMovies.ParseMovieById(epId, crLocale);

        if (movie != null){
            var movieMeta = CrunchyrollManager.Instance.CrMovies.EpisodeMeta(movie, dubLang);

            if (movieMeta != null){
                movieMeta.DownloadSubs = CrunchyrollManager.Instance.CrunOptions.DlSubs;
                movieMeta.OnlySubs = episodeDownloadMode == EpisodeDownloadMode.OnlySubs;

                var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);

                if (newOptions == null){
                    Console.Error.WriteLine("Failed to create a copy of your current settings");
                    MessageBus.Current.SendMessage(new ToastMessage($"Couldn't add episode to the queue, check the logs", ToastType.Error, 2));
                    return;
                }

                switch (episodeDownloadMode){
                    case EpisodeDownloadMode.OnlyVideo:
                        newOptions.Novids = false;
                        newOptions.Noaudio = true;
                        movieMeta.DownloadSubs = ["none"];
                        break;
                    case EpisodeDownloadMode.OnlyAudio:
                        newOptions.Novids = true;
                        newOptions.Noaudio = false;
                        movieMeta.DownloadSubs = ["none"];
                        break;
                    case EpisodeDownloadMode.OnlySubs:
                        newOptions.Novids = true;
                        newOptions.Noaudio = true;
                        break;
                    case EpisodeDownloadMode.Default:
                    default:
                        break;
                }

                newOptions.DubLang = dubLang;

                movieMeta.DownloadSettings = newOptions;

                movieMeta.VideoQuality = CrunchyrollManager.Instance.CrunOptions.QualityVideo;

                if (newOptions.DownloadOnlyWithAllSelectedDubSub){
                    if (!movieMeta.DownloadSubs.Contains("none") && !movieMeta.DownloadSubs.Contains("all") && !movieMeta.DownloadSubs.All(item => (movieMeta.AvailableSubs ?? []).Contains(item))){
                        //missing subs
                        Console.Error.WriteLine($"Episode not added because of missing subs - {movieMeta.SeasonTitle} - Season {movieMeta.Season} - {movieMeta.EpisodeTitle}");
                        return;
                    }

                    if (movieMeta.Data.Count < dubLang.Count && !CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub){
                        //missing dubs
                        Console.Error.WriteLine($"Episode not added because of missing dubs - {movieMeta.SeasonTitle} - Season {movieMeta.Season} - {movieMeta.EpisodeTitle}");
                        return;
                    }
                }

                Queue.Add(movieMeta);

                Console.WriteLine("Added Movie to Queue");
                MessageBus.Current.SendMessage(new ToastMessage($"Added Movie to Queue", ToastType.Information, 1));
                return;
            }
        }

        Console.Error.WriteLine($"No episode or movie found with the id: {epId}");
        MessageBus.Current.SendMessage(new ToastMessage($"Couldn't add episode to the queue - No episode or movie found with the id: {epId}", ToastType.Error, 3));
    }


    public void CrAddMusicMetaToQueue(CrunchyEpMeta epMeta){
        var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);
        epMeta.DownloadSettings = newOptions;

        Queue.Add(epMeta);
        MessageBus.Current.SendMessage(new ToastMessage($"Added episode to the queue", ToastType.Information, 1));
    }

    public async Task CrAddMusicVideoToQueue(string epId, string overrideDownloadPath = ""){
        await CrunchyrollManager.Instance.CrAuthEndpoint1.RefreshToken(true);

        var musicVideo = await CrunchyrollManager.Instance.CrMusic.ParseMusicVideoByIdAsync(epId, "");

        if (musicVideo != null){
            var musicVideoMeta = CrunchyrollManager.Instance.CrMusic.EpisodeMeta(musicVideo);

            (HistoryEpisode? historyEpisode, List<string> dublist, List<string> sublist, string downloadDirPath, string videoQuality) historyEpisode = (null, [], [], "", "");

            if (CrunchyrollManager.Instance.CrunOptions.History){
                historyEpisode = CrunchyrollManager.Instance.History.GetHistoryEpisodeWithDubListAndDownloadDir(musicVideoMeta.SeriesId, musicVideoMeta.SeasonId, musicVideoMeta.Data.First().MediaId);
            }

            musicVideoMeta.DownloadPath = !string.IsNullOrEmpty(overrideDownloadPath) ? overrideDownloadPath : (!string.IsNullOrEmpty(historyEpisode.downloadDirPath) ? historyEpisode.downloadDirPath : "");
            musicVideoMeta.VideoQuality = !string.IsNullOrEmpty(historyEpisode.videoQuality) ? historyEpisode.videoQuality : CrunchyrollManager.Instance.CrunOptions.QualityVideo;

            var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);
            musicVideoMeta.DownloadSettings = newOptions;

            Queue.Add(musicVideoMeta);
            MessageBus.Current.SendMessage(new ToastMessage($"Added music video to the queue", ToastType.Information, 1));
        }
    }

    public async Task CrAddConcertToQueue(string epId, string overrideDownloadPath = ""){
        await CrunchyrollManager.Instance.CrAuthEndpoint1.RefreshToken(true);

        var concert = await CrunchyrollManager.Instance.CrMusic.ParseConcertByIdAsync(epId, "");

        if (concert != null){
            var concertMeta = CrunchyrollManager.Instance.CrMusic.EpisodeMeta(concert);

            (HistoryEpisode? historyEpisode, List<string> dublist, List<string> sublist, string downloadDirPath, string videoQuality) historyEpisode = (null, [], [], "", "");

            if (CrunchyrollManager.Instance.CrunOptions.History){
                historyEpisode = CrunchyrollManager.Instance.History.GetHistoryEpisodeWithDubListAndDownloadDir(concertMeta.SeriesId, concertMeta.SeasonId, concertMeta.Data.First().MediaId);
            }

            concertMeta.DownloadPath = !string.IsNullOrEmpty(overrideDownloadPath) ? overrideDownloadPath : (!string.IsNullOrEmpty(historyEpisode.downloadDirPath) ? historyEpisode.downloadDirPath : "");
            concertMeta.VideoQuality = !string.IsNullOrEmpty(historyEpisode.videoQuality) ? historyEpisode.videoQuality : CrunchyrollManager.Instance.CrunOptions.QualityVideo;

            var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);
            concertMeta.DownloadSettings = newOptions;

            Queue.Add(concertMeta);
            MessageBus.Current.SendMessage(new ToastMessage($"Added concert to the queue", ToastType.Information, 1));
        }
    }


    public async Task CrAddSeriesToQueue(CrunchySeriesList list, CrunchyMultiDownload data){
        var selected = CrunchyrollManager.Instance.CrSeries.ItemSelectMultiDub(list.Data, data.DubLang, data.AllEpisodes, data.E);

        var failed = false;
        var partialAdd = false;


        foreach (var crunchyEpMeta in selected.Values.ToList()){
            if (crunchyEpMeta.Data.FirstOrDefault() != null){
                if (CrunchyrollManager.Instance.CrunOptions.History){
                    var historyEpisode = CrunchyrollManager.Instance.History.GetHistoryEpisodeWithDownloadDir(crunchyEpMeta.SeriesId, crunchyEpMeta.SeasonId, crunchyEpMeta.Data.First().MediaId);
                    if (CrunchyrollManager.Instance.CrunOptions.SonarrProperties is{ SonarrEnabled: true, UseSonarrNumbering: true }){
                        if (historyEpisode.historyEpisode != null){
                            if (!string.IsNullOrEmpty(historyEpisode.historyEpisode.SonarrEpisodeNumber)){
                                crunchyEpMeta.EpisodeNumber = historyEpisode.historyEpisode.SonarrEpisodeNumber;
                            }

                            if (!string.IsNullOrEmpty(historyEpisode.historyEpisode.SonarrSeasonNumber)){
                                crunchyEpMeta.Season = historyEpisode.historyEpisode.SonarrSeasonNumber;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(historyEpisode.downloadDirPath)){
                        crunchyEpMeta.DownloadPath = historyEpisode.downloadDirPath;
                    }
                }

                if (CrunchyrollManager.Instance.CrunOptions.IncludeVideoDescription){
                    if (crunchyEpMeta.Data is{ Count: > 0 }){
                        var episode = await CrunchyrollManager.Instance.CrEpisode.ParseEpisodeById(crunchyEpMeta.Data.First().MediaId,
                            string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.DescriptionLang) ? CrunchyrollManager.Instance.DefaultLocale : CrunchyrollManager.Instance.CrunOptions.DescriptionLang, true);
                        crunchyEpMeta.Description = episode?.Description ?? crunchyEpMeta.Description;
                    }
                }

                var subLangList = CrunchyrollManager.Instance.History.GetSubList(crunchyEpMeta.SeriesId, crunchyEpMeta.SeasonId);

                crunchyEpMeta.VideoQuality = !string.IsNullOrEmpty(subLangList.videoQuality) ? subLangList.videoQuality : CrunchyrollManager.Instance.CrunOptions.QualityVideo;
                crunchyEpMeta.DownloadSubs = subLangList.sublist.Count > 0 ? subLangList.sublist : CrunchyrollManager.Instance.CrunOptions.DlSubs;


                if (CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub && crunchyEpMeta.Data.Count > 1){
                    var sortedMetaData = crunchyEpMeta.Data
                        .OrderBy(metaData => {
                            var locale = metaData.Lang?.CrLocale ?? string.Empty;
                            var index = data.DubLang.IndexOf(locale);
                            return index != -1 ? index : int.MaxValue;
                        })
                        .ToList();

                    if (sortedMetaData.Count != 0){
                        var first = sortedMetaData.First();
                        crunchyEpMeta.Data = [first];
                        crunchyEpMeta.SelectedDubs = [first.Lang?.CrLocale ?? string.Empty];
                    }
                }

                var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);

                if (newOptions == null){
                    Console.Error.WriteLine("Failed to create a copy of your current settings");
                    MessageBus.Current.SendMessage(new ToastMessage($"Couldn't add episode to the queue, check the logs", ToastType.Error, 2));
                    return;
                }

                if (crunchyEpMeta.OnlySubs){
                    newOptions.Novids = true;
                    newOptions.Noaudio = true;
                }

                newOptions.DubLang = data.DubLang;

                crunchyEpMeta.DownloadSettings = newOptions;

                if (!crunchyEpMeta.DownloadSubs.Contains("none") && crunchyEpMeta.DownloadSubs.All(item => (crunchyEpMeta.AvailableSubs ?? []).Contains(item))){
                    if (!(crunchyEpMeta.Data.Count < data.DubLang.Count && !CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub)){
                        crunchyEpMeta.HighlightAllAvailable = true;
                    }
                }

                if (newOptions.DownloadOnlyWithAllSelectedDubSub){
                    if (!crunchyEpMeta.DownloadSubs.Contains("none") && !crunchyEpMeta.DownloadSubs.Contains("all") && !crunchyEpMeta.DownloadSubs.All(item => (crunchyEpMeta.AvailableSubs ?? []).Contains(item))){
                        //missing subs
                        Console.Error.WriteLine($"Episode not added because of missing subs - {crunchyEpMeta.SeasonTitle} - Season {crunchyEpMeta.Season} - {crunchyEpMeta.EpisodeTitle}");
                        continue;
                    }

                    if (crunchyEpMeta.Data.Count < data.DubLang.Count && !CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub){
                        //missing dubs
                        Console.Error.WriteLine($"Episode not added because of missing dubs - {crunchyEpMeta.SeasonTitle} - Season {crunchyEpMeta.Season} - {crunchyEpMeta.EpisodeTitle}");
                        continue;
                    }
                }

                Queue.Add(crunchyEpMeta);

                if (crunchyEpMeta.Data.Count < data.DubLang.Count && !CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub){
                    Console.WriteLine("Added Episode to Queue but couldn't find all selected dubs");
                    Console.Error.WriteLine("Added Episode to Queue but couldn't find all selected dubs - Available dubs/subs: ");

                    partialAdd = true;

                    var languages = (crunchyEpMeta.Data.First().Versions ?? []).Select(version => $"{(version.IsPremiumOnly ? "+ " : "")}{version.AudioLocale}").ToArray();

                    Console.Error.WriteLine(
                        $"{crunchyEpMeta.SeasonTitle} - Season {crunchyEpMeta.Season} - {crunchyEpMeta.EpisodeTitle} dubs - [{string.Join(", ", languages)}] subs - [{string.Join(", ", crunchyEpMeta.AvailableSubs ?? [])}]");
                    MessageBus.Current.SendMessage(new ToastMessage($"Added episode to the queue but couldn't find all selected dubs", ToastType.Warning, 2));
                }
            } else{
                failed = true;
            }
        }

        if (failed && !partialAdd){
            MainWindow.Instance.ShowError("Not all episodes could be added – make sure that you are signed in with an account that has an active premium subscription?");
        } else if (selected.Values.Count > 0 && !partialAdd){
            MessageBus.Current.SendMessage(new ToastMessage($"Added episodes to the queue", ToastType.Information, 1));
        } else if (!partialAdd){
            MessageBus.Current.SendMessage(new ToastMessage($"Couldn't add episode(s) to the queue with current dub settings", ToastType.Error, 2));
        }
    }
}