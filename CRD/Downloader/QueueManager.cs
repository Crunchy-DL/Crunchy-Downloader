using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.CustomList;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.ViewModels;
using CRD.Views;
using ReactiveUI;

namespace CRD.Downloader;

public class QueueManager{
    #region Download Variables

    public RefreshableObservableCollection<CrunchyEpMeta> Queue = new RefreshableObservableCollection<CrunchyEpMeta>();
    public ObservableCollection<DownloadItemModel> DownloadItemModels = new ObservableCollection<DownloadItemModel>();
    public int ActiveDownloads;

    #endregion


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
                    var downloadItem = DownloadItemModels.FirstOrDefault(e => e.epMeta.Equals(eOldItem));
                    if (downloadItem != null){
                        DownloadItemModels.Remove(downloadItem);
                    } else{
                        Console.Error.WriteLine("Failed to Remove Episode from list");
                    }
                }
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
    }


    public async Task CrAddEpisodeToQueue(string epId, string crLocale, List<string> dubLang, bool updateHistory = false, bool onlySubs = false){
        await CrunchyrollManager.Instance.CrAuth.RefreshToken(true);

        var episodeL = await CrunchyrollManager.Instance.CrEpisode.ParseEpisodeById(epId, crLocale);


        if (episodeL != null){
            if (episodeL.Value.IsPremiumOnly && !CrunchyrollManager.Instance.Profile.HasPremium){
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

                Queue.Add(selected);

                if (selected.Data.Count < dubLang.Count){
                    Console.WriteLine("Added Episode to Queue but couldn't find all selected dubs");
                    Console.Error.WriteLine("Added Episode to Queue but couldn't find all selected dubs - Available dubs/subs: ");

                    var languages = sList.EpisodeAndLanguages.Items.Select((a, index) =>
                        $"{(a.IsPremiumOnly ? "+ " : "")}{sList.EpisodeAndLanguages.Langs.ElementAtOrDefault(index).CrLocale ?? "Unknown"}").ToArray();

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
                    $"{(a.IsPremiumOnly ? "+ " : "")}{sList.EpisodeAndLanguages.Langs.ElementAtOrDefault(index).CrLocale ?? "Unknown"}").ToArray();

                Console.Error.WriteLine($"{selected.SeasonTitle} - Season {selected.Season} - {selected.EpisodeTitle} dubs - [{string.Join(", ", languages)}] subs - [{string.Join(", ", selected.AvailableSubs ??[])}]");
                MessageBus.Current.SendMessage(new ToastMessage($"Couldn't add episode to the queue with current dub settings", ToastType.Error, 2));
            }
        } else{
            Console.WriteLine("Couldn't find episode trying to find movie with id");

            var movie = await CrunchyrollManager.Instance.CrMovies.ParseMovieById(epId, crLocale);

            if (movie != null){
                var movieMeta = CrunchyrollManager.Instance.CrMovies.EpisodeMeta(movie, dubLang);

                if (movieMeta != null){
                    movieMeta.DownloadSubs = CrunchyrollManager.Instance.CrunOptions.DlSubs;
                    movieMeta.OnlySubs = onlySubs;
                    Queue.Add(movieMeta);

                    Console.WriteLine("Added Movie to Queue");
                    MessageBus.Current.SendMessage(new ToastMessage($"Added Movie to Queue", ToastType.Information, 1));
                }
            }
        }
    }


    public void CrAddMusicMetaToQueue(CrunchyEpMeta epMeta){
        Queue.Add(epMeta);
        MessageBus.Current.SendMessage(new ToastMessage($"Added episode to the queue", ToastType.Information, 1));
    }

    public async Task CrAddMusicVideoToQueue(string epId){
        await CrunchyrollManager.Instance.CrAuth.RefreshToken(true);

        var musicVideo = await CrunchyrollManager.Instance.CrMusic.ParseMusicVideoByIdAsync(epId, "");

        if (musicVideo != null){
            var musicVideoMeta = CrunchyrollManager.Instance.CrMusic.EpisodeMeta(musicVideo);
            Queue.Add(musicVideoMeta);
            MessageBus.Current.SendMessage(new ToastMessage($"Added music video to the queue", ToastType.Information, 1));
        }
    }

    public async Task CrAddConcertToQueue(string epId){
        await CrunchyrollManager.Instance.CrAuth.RefreshToken(true);

        var concert = await CrunchyrollManager.Instance.CrMusic.ParseConcertByIdAsync(epId, "");

        if (concert != null){
            var concertMeta = CrunchyrollManager.Instance.CrMusic.EpisodeMeta(concert);
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
                    var historyEpisode = CrunchyrollManager.Instance.History.GetHistoryEpisodeWithDownloadDir(crunchyEpMeta.ShowId, crunchyEpMeta.SeasonId, crunchyEpMeta.Data.First().MediaId);
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

                var subLangList = CrunchyrollManager.Instance.History.GetSubList(crunchyEpMeta.ShowId, crunchyEpMeta.SeasonId);

                crunchyEpMeta.VideoQuality = !string.IsNullOrEmpty(subLangList.videoQuality) ? subLangList.videoQuality : CrunchyrollManager.Instance.CrunOptions.QualityVideo;
                crunchyEpMeta.DownloadSubs = subLangList.sublist.Count > 0 ? subLangList.sublist : CrunchyrollManager.Instance.CrunOptions.DlSubs;


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