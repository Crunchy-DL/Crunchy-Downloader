using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.CustomList;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll;
using CRD.Utils.Structs.History;
using CRD.ViewModels;
using CRD.Views;
using ReactiveUI;

namespace CRD.Downloader;

public partial class QueueManager : ObservableObject{
    #region Download Variables

    public RefreshableObservableCollection<CrunchyEpMeta> Queue = new RefreshableObservableCollection<CrunchyEpMeta>();
    public ObservableCollection<DownloadItemModel> DownloadItemModels = new ObservableCollection<DownloadItemModel>();
    public int ActiveDownloads;

    #endregion

    [ObservableProperty]
    private bool _hasFailedItem;

    #region Singelton

    private static QueueManager? _instance;
    private static readonly object Padlock = new();

    public static QueueManager Instance{
        get{
            if (_instance == null){
                lock (Padlock){
                    if (_instance == null){
                        _instance = new QueueManager();
                    }
                }
            }

            return _instance;
        }
    }

    #endregion

    public QueueManager(){
        Queue.CollectionChanged += UpdateItemListOnRemove;
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

    public void UpdateDownloadListItems(){
        var list = Queue;

        foreach (CrunchyEpMeta crunchyEpMeta in list){
            var downloadItem = DownloadItemModels.FirstOrDefault(e => e.epMeta.Equals(crunchyEpMeta));
            if (downloadItem != null){
                downloadItem.Refresh();
            } else{
                downloadItem = new DownloadItemModel(crunchyEpMeta);
                _ = downloadItem.LoadImage();
                DownloadItemModels.Add(downloadItem);
            }

            if (downloadItem is{ isDownloading: false, Error: false } && CrunchyrollManager.Instance.CrunOptions.AutoDownload && ActiveDownloads < CrunchyrollManager.Instance.CrunOptions.SimultaneousDownloads){
                downloadItem.StartDownload();
            }
        }

        HasFailedItem = Queue.Any(item => item.DownloadProgress.Error);
    }


    public async Task CrAddEpisodeToQueue(string epId, string crLocale, List<string> dubLang, bool updateHistory = false, bool onlySubs = false){
        if (string.IsNullOrEmpty(epId)){
            return;
        }

        await CrunchyrollManager.Instance.CrAuth.RefreshToken(true);

        var episodeL = await CrunchyrollManager.Instance.CrEpisode.ParseEpisodeById(epId, crLocale);


        if (episodeL != null){
            if (episodeL.IsPremiumOnly && !CrunchyrollManager.Instance.Profile.HasPremium){
                MessageBus.Current.SendMessage(new ToastMessage($"Episode is a premium episode – make sure that you are signed in with an account that has an active premium subscription", ToastType.Error, 3));
                return;
            }

            var sList = await CrunchyrollManager.Instance.CrEpisode.EpisodeData((CrunchyEpisode)episodeL, updateHistory);

            (HistoryEpisode? historyEpisode, List<string> dublist, List<string> sublist, string downloadDirPath, string videoQuality) historyEpisode = (null, [], [], "", "");

            if (CrunchyrollManager.Instance.CrunOptions.History){
                var episode = sList.EpisodeAndLanguages.Items.First();
                historyEpisode = CrunchyrollManager.Instance.History.GetHistoryEpisodeWithDubListAndDownloadDir(episode.SeriesId, episode.SeasonId, episode.Id);
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

                selected.OnlySubs = onlySubs;

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
                        selected.Data =[first];
                        selected.SelectedDubs =[first.Lang?.CrLocale ?? string.Empty];
                    }
                }

                var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);

                if (selected.OnlySubs){
                    newOptions.Novids = true;
                    newOptions.Noaudio = true;
                }

                newOptions.DubLang = dubLang;

                selected.DownloadSettings = newOptions;

                Queue.Add(selected);


                if (selected.Data.Count < dubLang.Count && !CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub){
                    Console.WriteLine("Added Episode to Queue but couldn't find all selected dubs");
                    Console.Error.WriteLine("Added Episode to Queue but couldn't find all selected dubs - Available dubs/subs: ");

                    var languages = sList.EpisodeAndLanguages.Items.Select((a, index) =>
                        $"{(a.IsPremiumOnly ? "+ " : "")}{sList.EpisodeAndLanguages.Langs.ElementAtOrDefault(index)?.CrLocale ?? "Unknown"}").ToArray();

                    Console.Error.WriteLine(
                        $"{selected.SeasonTitle} - Season {selected.Season} - {selected.EpisodeTitle} dubs - [{string.Join(", ", languages)}] subs - [{string.Join(", ", selected.AvailableSubs ??[])}]");
                    MessageBus.Current.SendMessage(new ToastMessage($"Added episode to the queue but couldn't find all selected dubs", ToastType.Warning, 2));
                } else{
                    Console.WriteLine("Added Episode to Queue");
                    MessageBus.Current.SendMessage(new ToastMessage($"Added episode to the queue", ToastType.Information, 1));
                }
            } else{
                Console.WriteLine("Episode couldn't be added to Queue");
                Console.Error.WriteLine("Episode couldn't be added to Queue - Available dubs/subs: ");

                var languages = sList.EpisodeAndLanguages.Items.Select((a, index) =>
                    $"{(a.IsPremiumOnly ? "+ " : "")}{sList.EpisodeAndLanguages.Langs.ElementAtOrDefault(index)?.CrLocale ?? "Unknown"}").ToArray();

                Console.Error.WriteLine($"{selected.SeasonTitle} - Season {selected.Season} - {selected.EpisodeTitle} dubs - [{string.Join(", ", languages)}] subs - [{string.Join(", ", selected.AvailableSubs ??[])}]");
                MessageBus.Current.SendMessage(new ToastMessage($"Couldn't add episode to the queue with current dub settings", ToastType.Error, 2));
            }

            return;
        }

        Console.WriteLine("Couldn't find episode trying to find movie with id");

        var movie = await CrunchyrollManager.Instance.CrMovies.ParseMovieById(epId, crLocale);

        if (movie != null){
            var movieMeta = CrunchyrollManager.Instance.CrMovies.EpisodeMeta(movie, dubLang);

            if (movieMeta != null){
                movieMeta.DownloadSubs = CrunchyrollManager.Instance.CrunOptions.DlSubs;
                movieMeta.OnlySubs = onlySubs;

                var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);

                if (movieMeta.OnlySubs){
                    newOptions.Novids = true;
                    newOptions.Noaudio = true;
                }

                newOptions.DubLang = dubLang;

                movieMeta.DownloadSettings = newOptions;

                movieMeta.VideoQuality = CrunchyrollManager.Instance.CrunOptions.QualityVideo;

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

    public async Task CrAddMusicVideoToQueue(string epId){
        await CrunchyrollManager.Instance.CrAuth.RefreshToken(true);

        var musicVideo = await CrunchyrollManager.Instance.CrMusic.ParseMusicVideoByIdAsync(epId, "");

        if (musicVideo != null){
            var musicVideoMeta = CrunchyrollManager.Instance.CrMusic.EpisodeMeta(musicVideo);

            (HistoryEpisode? historyEpisode, List<string> dublist, List<string> sublist, string downloadDirPath, string videoQuality) historyEpisode = (null, [], [], "", "");

            if (CrunchyrollManager.Instance.CrunOptions.History){
                historyEpisode = CrunchyrollManager.Instance.History.GetHistoryEpisodeWithDubListAndDownloadDir(musicVideoMeta.SeriesId, musicVideoMeta.SeasonId, musicVideoMeta.Data.First().MediaId);
            }

            musicVideoMeta.VideoQuality = !string.IsNullOrEmpty(historyEpisode.videoQuality) ? historyEpisode.videoQuality : CrunchyrollManager.Instance.CrunOptions.QualityVideo;

            var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);
            musicVideoMeta.DownloadSettings = newOptions;

            Queue.Add(musicVideoMeta);
            MessageBus.Current.SendMessage(new ToastMessage($"Added music video to the queue", ToastType.Information, 1));
        }
    }

    public async Task CrAddConcertToQueue(string epId){
        await CrunchyrollManager.Instance.CrAuth.RefreshToken(true);

        var concert = await CrunchyrollManager.Instance.CrMusic.ParseConcertByIdAsync(epId, "");

        if (concert != null){
            var concertMeta = CrunchyrollManager.Instance.CrMusic.EpisodeMeta(concert);

            (HistoryEpisode? historyEpisode, List<string> dublist, List<string> sublist, string downloadDirPath, string videoQuality) historyEpisode = (null, [], [], "", "");

            if (CrunchyrollManager.Instance.CrunOptions.History){
                historyEpisode = CrunchyrollManager.Instance.History.GetHistoryEpisodeWithDubListAndDownloadDir(concertMeta.SeriesId, concertMeta.SeasonId, concertMeta.Data.First().MediaId);
            }

            concertMeta.VideoQuality = !string.IsNullOrEmpty(historyEpisode.videoQuality) ? historyEpisode.videoQuality : CrunchyrollManager.Instance.CrunOptions.QualityVideo;

            var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);
            concertMeta.DownloadSettings = newOptions;

            Queue.Add(concertMeta);
            MessageBus.Current.SendMessage(new ToastMessage($"Added concert to the queue", ToastType.Information, 1));
        }
    }


    public async Task CrAddSeriesToQueue(CrunchySeriesList list, CrunchyMultiDownload data){
        var selected = CrunchyrollManager.Instance.CrSeries.ItemSelectMultiDub(list.Data, data.DubLang, data.But, data.AllEpisodes, data.E);

        bool failed = false;

        foreach (var crunchyEpMeta in selected.Values.ToList()){
            if (crunchyEpMeta.Data?.First() != null){
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
                        crunchyEpMeta.Data =[first];
                        crunchyEpMeta.SelectedDubs =[first.Lang?.CrLocale ?? string.Empty];
                    }
                }

                var newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);

                if (crunchyEpMeta.OnlySubs){
                    newOptions.Novids = true;
                    newOptions.Noaudio = true;
                }

                newOptions.DubLang = data.DubLang;

                crunchyEpMeta.DownloadSettings = newOptions;


                Queue.Add(crunchyEpMeta);
            } else{
                failed = true;
            }
        }

        if (failed){
            MainWindow.Instance.ShowError("Not all episodes could be added – make sure that you are signed in with an account that has an active premium subscription?");
        } else{
            MessageBus.Current.SendMessage(new ToastMessage($"Added episodes to the queue", ToastType.Information, 1));
        }
    }
}