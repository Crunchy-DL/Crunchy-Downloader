using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Avalonia.Media;
using CRD.Utils;
using CRD.Utils.CustomList;
using CRD.Utils.DRM;
using CRD.Utils.Files;
using CRD.Utils.HLS;
using CRD.Utils.Muxing;
using CRD.Utils.Sonarr;
using CRD.Utils.Sonarr.Models;
using CRD.Utils.Structs;
using CRD.ViewModels;
using CRD.Views;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using LanguageItem = CRD.Utils.Structs.LanguageItem;

namespace CRD.Downloader;

public class Crunchyroll{
    public CrToken? Token;
    public CrCmsToken? CmsToken;

    public CrProfile Profile = new();
    public CrDownloadOptions CrunOptions;

    #region Download Variables

    public RefreshableObservableCollection<CrunchyEpMeta> Queue = new RefreshableObservableCollection<CrunchyEpMeta>();
    public ObservableCollection<DownloadItemModel> DownloadItemModels = new ObservableCollection<DownloadItemModel>();
    public int ActiveDownloads;
    public bool AutoDownload = false;

    #endregion


    #region Calendar Variables

    private Dictionary<string, CalendarWeek> calendar = new();
    private Dictionary<string, string> calendarLanguage = new();

    #endregion


    #region History Variables

    public ObservableCollection<HistorySeries> HistoryList = new();

    public HistorySeries SelectedSeries = new HistorySeries{
        Seasons =[]
    };

    public List<SonarrSeries> SonarrSeries =[];

    #endregion

    public string DefaultLocale = "en";

    public JsonSerializerSettings? SettingsJsonSerializerSettings = new(){
        NullValueHandling = NullValueHandling.Ignore,
    };

    private Widevine _widevine = Widevine.Instance;

    public CrAuth CrAuth;
    public CrEpisode CrEpisode;
    public CrSeries CrSeries;
    public History CrHistory;

    #region Singelton

    private static Crunchyroll? _instance;
    private static readonly object Padlock = new();

    public static Crunchyroll Instance{
        get{
            if (_instance == null){
                lock (Padlock){
                    if (_instance == null){
                        _instance = new Crunchyroll();
                    }
                }
            }

            return _instance;
        }
    }

    #endregion

    public async Task Init(){
        _widevine = Widevine.Instance;

        CrAuth = new CrAuth();
        CrEpisode = new CrEpisode();
        CrSeries = new CrSeries();
        CrHistory = new History();

        Profile = new CrProfile{
            Username = "???",
            Avatar = "003-cr-hime-excited.png",
            PreferredContentAudioLanguage = "ja-JP",
            PreferredContentSubtitleLanguage = "de-DE",
            HasPremium = false,
        };


        if (CfgManager.CheckIfFileExists(CfgManager.PathCrToken)){
            Token = CfgManager.DeserializeFromFile<CrToken>(CfgManager.PathCrToken);
            CrAuth.LoginWithToken();
        } else{
            await CrAuth.AuthAnonymous();
        }

        Console.WriteLine($"Can Decrypt: {_widevine.canDecrypt}");

        CrunOptions = new CrDownloadOptions();

        CrunOptions.Chapters = true;
        CrunOptions.Hslang = "none";
        CrunOptions.Force = "Y";
        CrunOptions.FileName = "${seriesTitle} - S${season}E${episode} [${height}p]";
        CrunOptions.Partsize = 10;
        CrunOptions.DlSubs = new List<string>{ "de-DE" };
        CrunOptions.Skipmux = false;
        CrunOptions.MkvmergeOptions = new List<string>{ "--no-date", "--disable-track-statistics-tags", "--engage no_variable_data" };
        CrunOptions.FfmpegOptions = new();
        CrunOptions.DefaultAudio = "ja-JP";
        CrunOptions.DefaultSub = "de-DE";
        CrunOptions.CcTag = "cc";
        CrunOptions.FsRetryTime = 5;
        CrunOptions.Numbers = 2;
        CrunOptions.Timeout = 15000;
        CrunOptions.DubLang = new List<string>(){ "ja-JP" };
        CrunOptions.SimultaneousDownloads = 2;
        CrunOptions.AccentColor = Colors.SlateBlue.ToString();
        CrunOptions.Theme = "System";
        CrunOptions.SelectedCalendarLanguage = "default";
        CrunOptions.DlVideoOnce = true;
        CrunOptions.StreamEndpoint = "web/firefox";
        CrunOptions.SubsAddScaledBorder = ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes;

        CrunOptions.History = true;

        CfgManager.UpdateSettingsFromFile();

        if (CrunOptions.History){
            if (File.Exists(CfgManager.PathCrHistory)){
                HistoryList = JsonConvert.DeserializeObject<ObservableCollection<HistorySeries>>(File.ReadAllText(CfgManager.PathCrHistory)) ??[];
            }

            RefreshSonarr();
        }

        if (CrunOptions.LogMode){
            CfgManager.EnableLogMode();
        } else{
            CfgManager.DisableLogMode();
        }

        calendarLanguage = new(){
            { "en-us", "https://www.crunchyroll.com/simulcastcalendar" },
            { "es", "https://www.crunchyroll.com/es/simulcastcalendar" },
            { "es-es", "https://www.crunchyroll.com/es-es/simulcastcalendar" },
            { "pt-br", "https://www.crunchyroll.com/pt-br/simulcastcalendar" },
            { "pt-pt", "https://www.crunchyroll.com/pt-pt/simulcastcalendar" },
            { "fr", "https://www.crunchyroll.com/fr/simulcastcalendar" },
            { "de", "https://www.crunchyroll.com/de/simulcastcalendar" },
            { "ar", "https://www.crunchyroll.com/ar/simulcastcalendar" },
            { "it", "https://www.crunchyroll.com/it/simulcastcalendar" },
            { "ru", "https://www.crunchyroll.com/ru/simulcastcalendar" },
            { "hi", "https://www.crunchyroll.com/hi/simulcastcalendar" },
        };
    }

    public async void RefreshSonarr(){
        await SonarrClient.Instance.CheckSonarrSettings();
        if (CrunOptions.SonarrProperties is{ SonarrEnabled: true }){
            SonarrSeries = await SonarrClient.Instance.GetSeries();
            CrHistory.MatchHistorySeriesWithSonarr(true);
        }
    }


    public async Task<CalendarWeek> GetCalendarForDate(string weeksMondayDate, bool forceUpdate){
        if (!forceUpdate && calendar.TryGetValue(weeksMondayDate, out var forDate)){
            return forDate;
        }

        var request = HttpClientReq.CreateRequestMessage($"{calendarLanguage[CrunOptions.SelectedCalendarLanguage ?? "de"]}?filter=premium&date={weeksMondayDate}", HttpMethod.Get, true, true, null);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        CalendarWeek week = new CalendarWeek();
        week.CalendarDays = new List<CalendarDay>();

        // Load the HTML content from a file
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(WebUtility.HtmlDecode(response.ResponseContent));

        // Select each 'li' element with class 'day'
        var dayNodes = doc.DocumentNode.SelectNodes("//li[contains(@class, 'day')]");

        if (dayNodes != null){
            foreach (var day in dayNodes){
                // Extract the date and day name
                var date = day.SelectSingleNode(".//time[@datetime]")?.GetAttributeValue("datetime", "No date");
                DateTime dayDateTime = DateTime.Parse(date, null, DateTimeStyles.RoundtripKind);

                if (week.FirstDayOfWeek == null){
                    week.FirstDayOfWeek = dayDateTime;
                    week.FirstDayOfWeekString = dayDateTime.ToString("yyyy-MM-dd");
                }

                var dayName = day.SelectSingleNode(".//h1[@class='day-name']/time")?.InnerText.Trim();

                CalendarDay calDay = new CalendarDay();

                calDay.CalendarEpisodes = new List<CalendarEpisode>();
                calDay.DayName = dayName;
                calDay.DateTime = dayDateTime;

                // Iterate through each episode listed under this day
                var episodes = day.SelectNodes(".//article[contains(@class, 'release')]");
                if (episodes != null){
                    foreach (var episode in episodes){
                        var episodeTimeStr = episode.SelectSingleNode(".//time[contains(@class, 'available-time')]")?.GetAttributeValue("datetime", null);
                        DateTime episodeTime = DateTime.Parse(episodeTimeStr, null, DateTimeStyles.RoundtripKind);
                        var hasPassed = DateTime.Now > episodeTime;

                        var episodeName = episode.SelectSingleNode(".//h1[contains(@class, 'episode-name')]")?.SelectSingleNode(".//cite[@itemprop='name']")?.InnerText.Trim();
                        var seasonLink = episode.SelectSingleNode(".//a[contains(@class, 'js-season-name-link')]")?.GetAttributeValue("href", "No link");
                        var episodeLink = episode.SelectSingleNode(".//a[contains(@class, 'available-episode-link')]")?.GetAttributeValue("href", "No link");
                        var thumbnailUrl = episode.SelectSingleNode(".//img[contains(@class, 'thumbnail')]")?.GetAttributeValue("src", "No image");
                        var isPremiumOnly = episode.SelectSingleNode(".//svg[contains(@class, 'premium-flag')]") != null;
                        var isPremiere = episode.SelectSingleNode(".//div[contains(@class, 'premiere-flag')]") != null;
                        var seasonName = episode.SelectSingleNode(".//a[contains(@class, 'js-season-name-link')]")?.SelectSingleNode(".//cite[@itemprop='name']")?.InnerText.Trim();
                        var episodeNumber = episode.SelectSingleNode(".//meta[contains(@itemprop, 'episodeNumber')]")?.GetAttributeValue("content", "?");

                        CalendarEpisode calEpisode = new CalendarEpisode();

                        calEpisode.DateTime = episodeTime;
                        calEpisode.HasPassed = hasPassed;
                        calEpisode.EpisodeName = episodeName;
                        calEpisode.SeasonUrl = seasonLink;
                        calEpisode.EpisodeUrl = episodeLink;
                        calEpisode.ThumbnailUrl = thumbnailUrl;
                        calEpisode.IsPremiumOnly = isPremiumOnly;
                        calEpisode.IsPremiere = isPremiere;
                        calEpisode.SeasonName = seasonName;
                        calEpisode.EpisodeNumber = episodeNumber;

                        calDay.CalendarEpisodes.Add(calEpisode);
                    }
                }

                week.CalendarDays.Add(calDay);
            }
        } else{
            Console.Error.WriteLine("No days found in the HTML document.");
        }

        calendar[weeksMondayDate] = week;


        return week;
    }

    public async Task AddEpisodeToQue(string epId, string locale, List<string> dubLang){
        await CrAuth.RefreshToken(true);

        var episodeL = await CrEpisode.ParseEpisodeById(epId, locale);


        if (episodeL != null){
            if (episodeL.Value.IsPremiumOnly && !Profile.HasPremium){
                MessageBus.Current.SendMessage(new ToastMessage($"Episode is a premium episode – make sure that you are signed in with an account that has an active premium subscription", ToastType.Error, 3));
                return;
            }

            var sList = await CrEpisode.EpisodeData((CrunchyEpisode)episodeL);
            var selected = CrEpisode.EpisodeMeta(sList, dubLang);

            if (selected.Data is{ Count: > 0 }){
                if (CrunOptions.History){
                    var historyEpisode = CrHistory.GetHistoryEpisodeWithDownloadDir(selected.ShowId, selected.SeasonId, selected.Data.First().MediaId);
                    if (CrunOptions.SonarrProperties is{ SonarrEnabled: true, UseSonarrNumbering: true }){
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

                Queue.Add(selected);


                if (selected.Data.Count < dubLang.Count){
                    Console.WriteLine("Added Episode to Queue but couldn't find all selected dubs");
                    MessageBus.Current.SendMessage(new ToastMessage($"Added episode to the queue but couldn't find all selected dubs", ToastType.Warning, 2));
                } else{
                    Console.WriteLine("Added Episode to Queue");
                    MessageBus.Current.SendMessage(new ToastMessage($"Added episode to the queue", ToastType.Information, 1));
                }
            } else{
                Console.WriteLine("Episode couldn't be added to Queue");
                MessageBus.Current.SendMessage(new ToastMessage($"Couldn't add episode to the queue with current dub settings", ToastType.Error, 2));
            }
        }
    }

    public void AddSeriesToQueue(CrunchySeriesList list, CrunchyMultiDownload data){
        var selected = CrSeries.ItemSelectMultiDub(list.Data, data.DubLang, data.But, data.AllEpisodes, data.E);

        bool failed = false;

        foreach (var crunchyEpMeta in selected.Values.ToList()){
            if (crunchyEpMeta.Data?.First().Playback != null){
                if (CrunOptions.History){
                    var historyEpisode = CrHistory.GetHistoryEpisodeWithDownloadDir(crunchyEpMeta.ShowId, crunchyEpMeta.SeasonId, crunchyEpMeta.Data.First().MediaId);
                    if (CrunOptions.SonarrProperties is{ SonarrEnabled: true, UseSonarrNumbering: true }){
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


    public async Task<bool> DownloadEpisode(CrunchyEpMeta data, CrDownloadOptions options){
        ActiveDownloads++;

        data.DownloadProgress = new DownloadProgress(){
            IsDownloading = true,
            Error = false,
            Percent = 0,
            Time = 0,
            DownloadSpeed = 0,
            Doing = "Starting"
        };
        Queue.Refresh();
        var res = await DownloadMediaList(data, options);

        if (res.Error){
            ActiveDownloads--;
            data.DownloadProgress = new DownloadProgress(){
                IsDownloading = false,
                Error = true,
                Percent = 100,
                Time = 0,
                DownloadSpeed = 0,
                Doing = "Download Error" + (!string.IsNullOrEmpty(res.ErrorText) ? " - " + res.ErrorText : ""),
            };
            Queue.Refresh();
            return false;
        }

        if (options.Skipmux == false){
            data.DownloadProgress = new DownloadProgress(){
                IsDownloading = true,
                Percent = 100,
                Time = 0,
                DownloadSpeed = 0,
                Doing = "Muxing"
            };

            Queue.Refresh();

            await MuxStreams(res.Data,
                new CrunchyMuxOptions{
                    FfmpegOptions = options.FfmpegOptions,
                    SkipSubMux = options.SkipSubsMux,
                    Output = res.FileName,
                    Mp4 = options.Mp4,
                    VideoTitle = res.VideoTitle,
                    Novids = options.Novids,
                    NoCleanup = options.Nocleanup,
                    DefaultAudio = Languages.FindLang(options.DefaultAudio),
                    DefaultSub = Languages.FindLang(options.DefaultSub),
                    MkvmergeOptions = options.MkvmergeOptions,
                    ForceMuxer = options.Force,
                    SyncTiming = options.SyncTiming,
                    CcTag = options.CcTag,
                    KeepAllVideos = true,
                    MuxDescription = options.IncludeVideoDescription
                },
                res.FileName);

            data.DownloadProgress = new DownloadProgress(){
                IsDownloading = true,
                Done = true,
                Percent = 100,
                Time = 0,
                DownloadSpeed = 0,
                Doing = "Done"
            };

            Queue.Refresh();
        } else{
            Console.WriteLine("Skipping mux");
        }

        ActiveDownloads--;

        if (CrunOptions.History && data.Data != null && data.Data.Count > 0){
            CrHistory.SetAsDownloaded(data.ShowId, data.SeasonId, data.Data.First().MediaId);
        }


        return true;
    }

    private async Task MuxStreams(List<DownloadedMedia> data, CrunchyMuxOptions options, string filename){
        var muxToMp3 = false;

        if (options.Novids == true || data.FindAll(a => a.Type == DownloadMediaType.Video).Count == 0){
            if (data.FindAll(a => a.Type == DownloadMediaType.Audio).Count > 0){
                Console.WriteLine("Mux to MP3");
                muxToMp3 = true;
            } else{
                Console.WriteLine("Skip muxing since no videos are downloaded");
                return;
            }
        }

        var subs = data.Where(a => a.Type == DownloadMediaType.Subtitle).ToList();
        var subsList = new List<SubtitleFonts>();

        foreach (var downloadedMedia in subs){
            var subt = new SubtitleFonts();
            subt.Language = downloadedMedia.Language;
            subt.Fonts = downloadedMedia.Fonts;
            subsList.Add(subt);
        }

        if (File.Exists($"{filename}.{(muxToMp3 ? "mp3" : options.Mp4 ? "mp4" : "mkv")}") && !string.IsNullOrEmpty(filename)){
            string newFilePath = filename;
            int counter = 1;

            while (File.Exists($"{newFilePath}.{(muxToMp3 ? "mp3" : options.Mp4 ? "mp4" : "mkv")}")){
                newFilePath = filename + $"({counter})";
                counter++;
            }

            filename = newFilePath;
        }

        bool muxDesc = false;
        if (options.MuxDescription){
            if (File.Exists($"{filename}.xml")){
                muxDesc = true;
            } else{
                Console.Error.WriteLine("No xml description file found to mux description");
            }
        }
        

        var merger = new Merger(new MergerOptions{
            OnlyVid = data.Where(a => a.Type == DownloadMediaType.Video).Select(a => new MergerInput{ Language = a.Lang, Path = a.Path ?? string.Empty }).ToList(),
            SkipSubMux = options.SkipSubMux,
            OnlyAudio = data.Where(a => a.Type == DownloadMediaType.Audio).Select(a => new MergerInput{ Language = a.Lang, Path = a.Path ?? string.Empty }).ToList(),
            Output = $"{filename}.{(muxToMp3 ? "mp3" : options.Mp4 ? "mp4" : "mkv")}",
            Subtitles = data.Where(a => a.Type == DownloadMediaType.Subtitle).Select(a => new SubtitleInput{ File = a.Path ?? string.Empty, Language = a.Language, ClosedCaption = a.Cc, Signs = a.Signs }).ToList(),
            KeepAllVideos = options.KeepAllVideos,
            Fonts = FontsManager.Instance.MakeFontsList(CfgManager.PathFONTS_DIR, subsList), // Assuming MakeFontsList is properly defined
            Chapters = data.Where(a => a.Type == DownloadMediaType.Chapters).Select(a => new MergerInput{ Language = a.Lang, Path = a.Path ?? string.Empty }).ToList(),
            VideoTitle = options.VideoTitle,
            Options = new MuxOptions(){
                ffmpeg = options.FfmpegOptions,
                mkvmerge = options.MkvmergeOptions
            },
            Defaults = new Defaults(){
                Audio = options.DefaultAudio,
                Sub = options.DefaultSub
            },
            CcTag = options.CcTag,
            mp3 = muxToMp3,
            MuxDescription = muxDesc
        });

        if (!File.Exists(CfgManager.PathFFMPEG)){
            Console.Error.WriteLine("FFmpeg not found");
        }

        if (!File.Exists(CfgManager.PathMKVMERGE)){
            Console.Error.WriteLine("MKVmerge not found");
        }

        bool isMuxed;

        // if (options.SyncTiming){
        //     await Merger.CreateDelays();
        // }

        if (!options.Mp4 && !muxToMp3){
            await merger.Merge("mkvmerge", CfgManager.PathMKVMERGE);
            isMuxed = true;
        } else{
            await merger.Merge("ffmpeg", CfgManager.PathFFMPEG);
            isMuxed = true;
        }

        if (isMuxed && options.NoCleanup == false){
            merger.CleanUp();
        }
    }

    private async Task<DownloadResponse> DownloadMediaList(CrunchyEpMeta data, CrDownloadOptions options){
        if (CmsToken?.Cms == null){
            Console.WriteLine("Missing CMS Token");
            MainWindow.Instance.ShowError("Missing CMS Token - are you signed in?");
            return new DownloadResponse{
                Data = new List<DownloadedMedia>(),
                Error = true,
                FileName = "./unknown",
                ErrorText = "Login problem"
            };
        }

        if (Profile.Username == "???"){
            MainWindow.Instance.ShowError("User Account not recognized - are you signed in?");
            return new DownloadResponse{
                Data = new List<DownloadedMedia>(),
                Error = true,
                FileName = "./unknown",
                ErrorText = "Login problem"
            };
        }

        if (!File.Exists(CfgManager.PathFFMPEG)){
            Console.Error.WriteLine("Missing ffmpeg");
            MainWindow.Instance.ShowError("FFmpeg not found");
            return new DownloadResponse{
                Data = new List<DownloadedMedia>(),
                Error = true,
                FileName = "./unknown",
                ErrorText = "Missing ffmpeg"
            };
        }

        if (!File.Exists(CfgManager.PathMKVMERGE)){
            Console.Error.WriteLine("Missing Mkvmerge");
            MainWindow.Instance.ShowError("Mkvmerge not found");
            return new DownloadResponse{
                Data = new List<DownloadedMedia>(),
                Error = true,
                FileName = "./unknown",
                ErrorText = "Missing Mkvmerge"
            };
        }

        if (!_widevine.canDecrypt){
            Console.Error.WriteLine("L3 key files missing");
            MainWindow.Instance.ShowError("Can't find CDM files in widevine folder ");
            return new DownloadResponse{
                Data = new List<DownloadedMedia>(),
                Error = true,
                FileName = "./unknown",
                ErrorText = "Missing L3 Key"
            };
        }

        if (!File.Exists(CfgManager.PathMP4Decrypt)){
            Console.Error.WriteLine("mp4decrypt  not found");
            MainWindow.Instance.ShowError("Can't find mp4decrypt in lib folder ");
            return new DownloadResponse{
                Data = new List<DownloadedMedia>(),
                Error = true,
                FileName = "./unknown",
                ErrorText = "Missing mp4decrypt"
            };
        }

        string mediaName = $"{data.SeasonTitle} - {data.EpisodeNumber} - {data.EpisodeTitle}";
        string fileName = "";
        var variables = new List<Variable>();

        List<DownloadedMedia> files = new List<DownloadedMedia>();

        if (data.Data != null && data.Data.All(a => a.Playback == null)){
            Console.WriteLine("No Video Data found - Are you trying to download a premium episode without havíng a premium account?");
            MainWindow.Instance.ShowError("No Video Data found - Are you trying to download a premium episode without havíng a premium account?");
            return new DownloadResponse{
                Data = files,
                Error = true,
                FileName = "./unknown",
                ErrorText = "Video Data not found"
            };
        }


        bool dlFailed = false;
        bool dlVideoOnce = false;
        string fileDir = CfgManager.PathVIDEOS_DIR;

        if (data.Data != null){
            foreach (CrunchyEpMetaData epMeta in data.Data){
                Console.WriteLine($"Requesting: [{epMeta.MediaId}] {mediaName}");

                fileDir = !string.IsNullOrEmpty(data.DownloadPath) ? data.DownloadPath : !string.IsNullOrEmpty(options.DownloadDirPath) ? options.DownloadDirPath : CfgManager.PathVIDEOS_DIR;

                if (!Helpers.IsValidPath(fileDir)){
                    fileDir = CfgManager.PathVIDEOS_DIR;
                }

                string currentMediaId = (epMeta.MediaId.Contains(':') ? epMeta.MediaId.Split(':')[1] : epMeta.MediaId);

                await CrAuth.RefreshToken(true);

                EpisodeVersion currentVersion = new EpisodeVersion();
                EpisodeVersion primaryVersion = new EpisodeVersion();
                bool isPrimary = epMeta.IsSubbed;

                //Get Media GUID
                string mediaId = epMeta.MediaId;
                string mediaGuid = currentMediaId;
                if (epMeta.Versions != null){
                    if (epMeta.Lang != null){
                        currentVersion = epMeta.Versions.Find(a => a.AudioLocale == epMeta.Lang?.CrLocale);
                    } else if (options.DubLang.Count == 1){
                        LanguageItem lang = Array.Find(Languages.languages, a => a.CrLocale == options.DubLang[0]);
                        currentVersion = epMeta.Versions.Find(a => a.AudioLocale == lang.CrLocale);
                    } else if (epMeta.Versions.Count == 1){
                        currentVersion = epMeta.Versions[0];
                    }

                    if (currentVersion.MediaGuid == null){
                        Console.WriteLine("Selected language not found in versions.");
                        MainWindow.Instance.ShowError("Selected language not found");
                        continue;
                    }

                    isPrimary = currentVersion.Original;
                    mediaId = currentVersion.MediaGuid;
                    mediaGuid = currentVersion.Guid;

                    if (!isPrimary){
                        primaryVersion = epMeta.Versions.Find(a => a.Original);
                    } else{
                        primaryVersion = currentVersion;
                    }
                }

                if (mediaId.Contains(':')){
                    mediaId = mediaId.Split(':')[1];
                }

                if (mediaGuid.Contains(':')){
                    mediaGuid = mediaGuid.Split(':')[1];
                }

                Console.WriteLine("MediaGuid: " + mediaId);

                #region Chapters

                List<string> compiledChapters = new List<string>();

                if (options.Chapters){
                    await ParseChapters(primaryVersion.Guid, compiledChapters);
                }

                #endregion


                var fetchPlaybackData = await FetchPlaybackData(mediaId, mediaGuid, epMeta);

                if (!fetchPlaybackData.IsOk){
                    if (!fetchPlaybackData.IsOk && fetchPlaybackData.error != string.Empty){
                        var s = fetchPlaybackData.error;
                        var error = StreamError.FromJson(s);
                        if (error != null && error.IsTooManyActiveStreamsError()){
                            MainWindow.Instance.ShowError("Too many active streams that couldn't be stopped");
                            return new DownloadResponse{
                                Data = new List<DownloadedMedia>(),
                                Error = true,
                                FileName = "./unknown",
                                ErrorText = "Too many active streams that couldn't be stopped"
                            };
                        }
                    }

                    MainWindow.Instance.ShowError("Couldn't get Playback Data");
                    return new DownloadResponse{
                        Data = new List<DownloadedMedia>(),
                        Error = true,
                        FileName = "./unknown",
                        ErrorText = "Playback data not found"
                    };
                }

                var pbData = fetchPlaybackData.pbData;

                List<string> hsLangs = new List<string>();
                var pbStreams = pbData.Data?[0];
                var streams = new List<StreamDetailsPop>();

                variables.Add(new Variable("title", data.EpisodeTitle ?? string.Empty, true));
                variables.Add(new Variable("episode", (int.TryParse(data.EpisodeNumber, out int episodeNum) ? (object)episodeNum : data.AbsolutEpisodeNumberE) ?? string.Empty, false));
                variables.Add(new Variable("seriesTitle", data.SeriesTitle ?? string.Empty, true));
                variables.Add(new Variable("showTitle", data.SeasonTitle ?? string.Empty, true));
                variables.Add(new Variable("season", data.Season != null ? Math.Round(double.Parse(data.Season, CultureInfo.InvariantCulture), 1) : 0, false));

                if (pbStreams?.Keys != null){
                    foreach (var key in pbStreams.Keys){
                        if ((key.Contains("hls") || key.Contains("dash")) &&
                            !(key.Contains("hls") && key.Contains("drm")) &&
                            !((!_widevine.canDecrypt || !File.Exists(CfgManager.PathMP4Decrypt)) && key.Contains("drm")) &&
                            !key.Contains("trailer")){
                            var pb = pbStreams[key].Select(v => {
                                v.Value.HardsubLang = v.Value.HardsubLocale != null
                                    ? Languages.FixAndFindCrLc(v.Value.HardsubLocale.GetEnumMemberValue()).Locale
                                    : null;
                                if (v.Value.HardsubLocale != null && v.Value.HardsubLang != null && !hsLangs.Contains(v.Value.HardsubLocale.GetEnumMemberValue())){
                                    hsLangs.Add(v.Value.HardsubLang);
                                }

                                return new StreamDetailsPop{
                                    Url = v.Value.Url,
                                    HardsubLocale = v.Value.HardsubLocale,
                                    HardsubLang = v.Value.HardsubLang,
                                    AudioLang = v.Value.AudioLang,
                                    Type = v.Value.Type,
                                    Format = key,
                                };
                            }).ToList();

                            streams.AddRange(pb);
                        }
                    }

                    if (streams.Count < 1){
                        Console.WriteLine("No full streams found!");
                        MainWindow.Instance.ShowError("No streams found");
                        return new DownloadResponse{
                            Data = new List<DownloadedMedia>(),
                            Error = true,
                            FileName = "./unknown",
                            ErrorText = "Streams not found"
                        };
                    }

                    var audDub = "";
                    if (pbData.Meta != null){
                        audDub = Languages.FindLang(Languages.FixLanguageTag((pbData.Meta.AudioLocale ?? Locale.DefaulT).GetEnumMemberValue())).Code;
                    }

                    hsLangs = Languages.SortTags(hsLangs);

                    streams = streams.Select(s => {
                        s.AudioLang = audDub;
                        s.HardsubLang = string.IsNullOrEmpty(s.HardsubLang) ? "-" : s.HardsubLang;
                        s.Type = $"{s.Format}/{s.AudioLang}/{s.HardsubLang}";
                        return s;
                    }).ToList();

                    streams.Sort((a, b) => String.CompareOrdinal(a.Type, b.Type));

                    if (options.Hslang != "none"){
                        if (hsLangs.IndexOf(options.Hslang) > -1){
                            Console.WriteLine($"Selecting stream with {Languages.Locale2language(options.Hslang).Language} hardsubs");
                            streams = streams.Where((s) => s.HardsubLang != "-" && s.HardsubLang == options.Hslang).ToList();
                        } else{
                            Console.WriteLine($"Selected stream with {Languages.Locale2language(options.Hslang).Language} hardsubs not available");
                            if (hsLangs.Count > 0){
                                Console.WriteLine("Try hardsubs stream: " + string.Join(", ", hsLangs));
                            }

                            dlFailed = true;
                        }
                    } else{
                        streams = streams.Where((s) => {
                            if (s.HardsubLang != "-"){
                                return false;
                            }

                            return true;
                        }).ToList();

                        if (streams.Count < 1){
                            Console.WriteLine("Raw streams not available!");
                            if (hsLangs.Count > 0){
                                Console.WriteLine("Try hardsubs stream: " + string.Join(", ", hsLangs));
                            }

                            dlFailed = true;
                        }

                        Console.WriteLine("Selecting raw stream");
                    }

                    StreamDetailsPop? curStream = null;
                    if (!dlFailed){
                        options.Kstream = options.Kstream >= 1 && options.Kstream <= streams.Count
                            ? options.Kstream
                            : 1;

                        for (int i = 0; i < streams.Count; i++){
                            string isSelected = options.Kstream == i + 1 ? "+" : " ";
                            Console.WriteLine($"Full stream found! ({isSelected}{i + 1}: {streams[i].Type})");
                        }

                        Console.WriteLine("Downloading video...");
                        curStream = streams[options.Kstream - 1];

                        Console.WriteLine($"Playlists URL: {curStream.Url} ({curStream.Type})");
                    }

                    string tsFile = "";

                    if (!dlFailed && curStream != null && options is not{ Novids: true, Noaudio: true }){
                        var streamPlaylistsReq = HttpClientReq.CreateRequestMessage(curStream.Url ?? string.Empty, HttpMethod.Get, true, true, null);

                        var streamPlaylistsReqResponse = await HttpClientReq.Instance.SendHttpRequest(streamPlaylistsReq);

                        if (!streamPlaylistsReqResponse.IsOk){
                            dlFailed = true;
                            return new DownloadResponse{
                                Data = new List<DownloadedMedia>(),
                                Error = dlFailed,
                                FileName = "./unknown",
                                ErrorText = "Playlist fetch problem"
                            };
                        }

                        if (dlFailed){
                            Console.WriteLine($"CAN\'T FETCH VIDEO PLAYLISTS!");
                        } else{
                            if (streamPlaylistsReqResponse.ResponseContent.Contains("MPD")){
                                var match = Regex.Match(curStream.Url ?? string.Empty, @"(.*\.urlset\/)");
                                var matchedUrl = match.Success ? match.Value : null;
                                //Parse MPD Playlists
                                var crLocal = "";
                                if (pbData.Meta != null){
                                    crLocal = Languages.FixLanguageTag((pbData.Meta.AudioLocale ?? Locale.DefaulT).GetEnumMemberValue());
                                }

                                MPDParsed streamPlaylists = MPDParser.Parse(streamPlaylistsReqResponse.ResponseContent, Languages.FindLang(crLocal), matchedUrl);

                                List<string> streamServers = new List<string>(streamPlaylists.Data.Keys);
                                options.StreamServer = options.StreamServer > streamServers.Count ? 1 : options.StreamServer;

                                if (streamServers.Count == 0){
                                    return new DownloadResponse{
                                        Data = new List<DownloadedMedia>(),
                                        Error = true,
                                        FileName = "./unknown",
                                        ErrorText = "No stream servers found"
                                    };
                                }

                                if (options.StreamServer == 0){
                                    options.StreamServer = 1;
                                }

                                string selectedServer = streamServers[options.StreamServer - 1];
                                ServerData selectedList = streamPlaylists.Data[selectedServer];

                                var videos = selectedList.video.Select(item => new VideoItem{
                                    segments = item.segments,
                                    pssh = item.pssh,
                                    quality = item.quality,
                                    bandwidth = item.bandwidth,
                                    resolutionText = $"{item.quality.width}x{item.quality.height} ({Math.Round(item.bandwidth / 1024.0)}KiB/s)"
                                }).ToList();

                                var audios = selectedList.audio.Select(item => new AudioItem{
                                    @default = item.@default,
                                    segments = item.segments,
                                    pssh = item.pssh,
                                    language = item.language,
                                    bandwidth = item.bandwidth,
                                    resolutionText = $"{Math.Round(item.bandwidth / 1000.0)}kB/s"
                                }).ToList();

                                videos.Sort((a, b) => a.quality.width.CompareTo(b.quality.width));
                                audios.Sort((a, b) => a.bandwidth.CompareTo(b.bandwidth));

                                int chosenVideoQuality;
                                if (options.QualityVideo == "best"){
                                    chosenVideoQuality = videos.Count;
                                } else if (options.QualityVideo == "worst"){
                                    chosenVideoQuality = 1;
                                } else{
                                    var tempIndex = videos.FindIndex(a => a.quality.height + "" == options.QualityVideo);
                                    if (tempIndex < 0){
                                        chosenVideoQuality = videos.Count;
                                    } else{
                                        tempIndex++;
                                        chosenVideoQuality = tempIndex;
                                    }
                                }

                                if (chosenVideoQuality > videos.Count){
                                    Console.WriteLine($"The requested quality of {chosenVideoQuality} is greater than the maximum {videos.Count}.\n[WARN] Therefore, the maximum will be capped at {videos.Count}.");
                                    chosenVideoQuality = videos.Count;
                                }

                                chosenVideoQuality--;

                                int chosenAudioQuality;
                                if (options.QualityAudio == "best"){
                                    chosenAudioQuality = audios.Count;
                                } else if (options.QualityAudio == "worst"){
                                    chosenAudioQuality = 1;
                                } else{
                                    var tempIndex = audios.FindIndex(a => a.resolutionText == options.QualityAudio);
                                    if (tempIndex < 0){
                                        chosenAudioQuality = audios.Count;
                                    } else{
                                        tempIndex++;
                                        chosenAudioQuality = tempIndex;
                                    }
                                }


                                if (chosenAudioQuality > audios.Count){
                                    chosenAudioQuality = audios.Count;
                                }

                                chosenAudioQuality--;

                                VideoItem chosenVideoSegments = videos[chosenVideoQuality];
                                AudioItem chosenAudioSegments = audios[chosenAudioQuality];

                                Console.WriteLine("Servers available:");
                                foreach (var server in streamServers){
                                    Console.WriteLine($"\t{server}");
                                }

                                Console.WriteLine("Available Video Qualities:");
                                for (int i = 0; i < videos.Count; i++){
                                    Console.WriteLine($"\t[{i + 1}] {videos[i].resolutionText}");
                                }

                                Console.WriteLine("Available Audio Qualities:");
                                for (int i = 0; i < audios.Count; i++){
                                    Console.WriteLine($"\t[{i + 1}] {audios[i].resolutionText}");
                                }

                                variables.Add(new Variable("height", chosenVideoSegments.quality.height, false));
                                variables.Add(new Variable("width", chosenVideoSegments.quality.width, false));

                                LanguageItem? lang = Languages.languages.FirstOrDefault(a => a.Code == curStream.AudioLang);
                                if (lang == null){
                                    Console.Error.WriteLine($"Unable to find language for code {curStream.AudioLang}");
                                    MainWindow.Instance.ShowError($"Unable to find language for code {curStream.AudioLang}");
                                    return new DownloadResponse{
                                        Data = new List<DownloadedMedia>(),
                                        Error = true,
                                        FileName = "./unknown",
                                        ErrorText = "Language not found"
                                    };
                                }

                                Console.WriteLine($"Selected quality:");
                                Console.WriteLine($"\tVideo: {chosenVideoSegments.resolutionText}");
                                Console.WriteLine($"\tAudio: {chosenAudioSegments.resolutionText}");
                                Console.WriteLine($"\tServer: {selectedServer}");
                                Console.WriteLine("Stream URL:" + chosenVideoSegments.segments[0].uri.Split(new[]{ ",.urlset" }, StringSplitOptions.None)[0]);


                                fileName = Path.Combine(FileNameManager.ParseFileName(options.FileName, variables, options.Numbers, options.Override).ToArray());
                                string outFile = Path.Combine(FileNameManager.ParseFileName(options.FileName + "." + (epMeta.Lang?.Name ?? lang.Value.Name), variables, options.Numbers, options.Override).ToArray());

                                string tempFile = Path.Combine(FileNameManager.ParseFileName($"temp-{(currentVersion.Guid != null ? currentVersion.Guid : currentMediaId)}", variables, options.Numbers, options.Override)
                                    .ToArray());
                                string tempTsFile = Path.IsPathRooted(tempFile) ? tempFile : Path.Combine(fileDir, tempFile);

                                bool audioDownloaded = false, videoDownloaded = false;

                                if (options.DlVideoOnce && dlVideoOnce){
                                    Console.WriteLine("Already downloaded video, skipping video download...");
                                } else if (options.Novids){
                                    Console.WriteLine("Skipping video download...");
                                } else{
                                    var videoDownloadResult = await DownloadVideo(chosenVideoSegments, options, outFile, tsFile, tempTsFile, data, fileDir);

                                    tsFile = videoDownloadResult.tsFile;

                                    if (!videoDownloadResult.Ok){
                                        Console.Error.WriteLine($"DL Stats: {JsonConvert.SerializeObject(videoDownloadResult.Parts)}");
                                        dlFailed = true;
                                    }

                                    dlVideoOnce = true;
                                    videoDownloaded = true;
                                }


                                if (chosenAudioSegments.segments.Count > 0 && !options.Noaudio && !dlFailed){
                                    var audioDownloadResult = await DownloadAudio(chosenAudioSegments, options, outFile, tsFile, tempTsFile, data, fileDir);

                                    tsFile = audioDownloadResult.tsFile;

                                    if (!audioDownloadResult.Ok){
                                        Console.Error.WriteLine($"DL Stats: {JsonConvert.SerializeObject(audioDownloadResult.Parts)}");
                                        dlFailed = true;
                                    }

                                    audioDownloaded = true;
                                } else if (options.Noaudio){
                                    Console.WriteLine("Skipping audio download...");
                                }

                                if (dlFailed){
                                    return new DownloadResponse{
                                        Data = files,
                                        Error = dlFailed,
                                        FileName = fileName.Length > 0 ? (Path.IsPathRooted(fileName) ? fileName : Path.Combine(fileDir, fileName)) : "./unknown",
                                        ErrorText = ""
                                    };
                                }

                                if ((chosenVideoSegments.pssh != null || chosenAudioSegments.pssh != null) && (videoDownloaded || audioDownloaded)){
                                    data.DownloadProgress = new DownloadProgress(){
                                        IsDownloading = true,
                                        Percent = 100,
                                        Time = 0,
                                        DownloadSpeed = 0,
                                        Doing = "Decrypting"
                                    };
                                    Queue.Refresh();

                                    var assetIdRegexMatch = Regex.Match(chosenVideoSegments.segments[0].uri, @"/assets/(?:p/)?([^_,]+)");
                                    var assetId = assetIdRegexMatch.Success ? assetIdRegexMatch.Groups[1].Value : null;
                                    var sessionId = Helpers.GenerateSessionId();

                                    Console.WriteLine("Decryption Needed, attempting to decrypt");

                                    if (!_widevine.canDecrypt){
                                        dlFailed = true;
                                        return new DownloadResponse{
                                            Data = files,
                                            Error = dlFailed,
                                            FileName = fileName.Length > 0 ? (Path.IsPathRooted(fileName) ? fileName : Path.Combine(fileDir, fileName)) : "./unknown",
                                            ErrorText = "Decryption Needed but couldn't find CDM files"
                                        };
                                    }


                                    var reqBodyData = new{
                                        accounting_id = "crunchyroll",
                                        asset_id = assetId,
                                        session_id = sessionId,
                                        user_id = Token?.account_id
                                    };

                                    var json = JsonConvert.SerializeObject(reqBodyData);
                                    var reqBody = new StringContent(json, Encoding.UTF8, "application/json");

                                    var decRequest = HttpClientReq.CreateRequestMessage($"{Api.DRM}", HttpMethod.Post, false, false, null);
                                    decRequest.Content = reqBody;

                                    var decRequestResponse = await HttpClientReq.Instance.SendHttpRequest(decRequest);

                                    if (!decRequestResponse.IsOk){
                                        Console.Error.WriteLine("Request to DRM Authentication failed: ");
                                        MainWindow.Instance.ShowError("Request to DRM Authentication failed");
                                        dlFailed = true;
                                        return new DownloadResponse{
                                            Data = files,
                                            Error = dlFailed,
                                            FileName = fileName.Length > 0 ? (Path.IsPathRooted(fileName) ? fileName : Path.Combine(fileDir, fileName)) : "./unknown",
                                            ErrorText = "DRM Authentication failed"
                                        };
                                    }

                                    DrmAuthData authData = Helpers.Deserialize<DrmAuthData>(decRequestResponse.ResponseContent, SettingsJsonSerializerSettings) ?? new DrmAuthData();

                                    Dictionary<string, string> authDataDict = new Dictionary<string, string>
                                        { { "dt-custom-data", authData.CustomData ?? string.Empty },{ "x-dt-auth-token", authData.Token ?? string.Empty } };

                                    var encryptionKeys = await _widevine.getKeys(chosenVideoSegments.pssh, "https://lic.drmtoday.com/license-proxy-widevine/cenc/", authDataDict);

                                    if (encryptionKeys.Count == 0){
                                        Console.Error.WriteLine("Failed to get encryption keys");
                                        dlFailed = true;
                                        return new DownloadResponse{
                                            Data = files,
                                            Error = dlFailed,
                                            FileName = fileName.Length > 0 ? (Path.IsPathRooted(fileName) ? fileName : Path.Combine(fileDir, fileName)) : "./unknown",
                                            ErrorText = "Couldn't get DRM encryption keys"
                                        };
                                    }


                                    if (Path.Exists(CfgManager.PathMP4Decrypt)){
                                        var keyId = BitConverter.ToString(encryptionKeys[0].KeyID).Replace("-", "").ToLower();
                                        var key = BitConverter.ToString(encryptionKeys[0].Bytes).Replace("-", "").ToLower();
                                        var commandBase = $"--show-progress --key {keyId}:{key}";
                                        var commandVideo = commandBase + $" \"{tempTsFile}.video.enc.m4s\" \"{tempTsFile}.video.m4s\"";
                                        var commandAudio = commandBase + $" \"{tempTsFile}.audio.enc.m4s\" \"{tempTsFile}.audio.m4s\"";
                                        if (videoDownloaded){
                                            Console.WriteLine("Started decrypting video");
                                            data.DownloadProgress = new DownloadProgress(){
                                                IsDownloading = true,
                                                Percent = 100,
                                                Time = 0,
                                                DownloadSpeed = 0,
                                                Doing = "Decrypting video"
                                            };
                                            Queue.Refresh();
                                            var decryptVideo = await Helpers.ExecuteCommandAsync("mp4decrypt", CfgManager.PathMP4Decrypt, commandVideo);

                                            if (!decryptVideo.IsOk){
                                                Console.Error.WriteLine($"Decryption failed with exit code {decryptVideo.ErrorCode}");
                                                MainWindow.Instance.ShowError($"Decryption failed with exit code {decryptVideo.ErrorCode}");
                                                try{
                                                    File.Move($"{tempTsFile}.video.enc.m4s", $"{tsFile}.video.enc.m4s");
                                                } catch (IOException ex){
                                                    Console.WriteLine($"An error occurred: {ex.Message}");
                                                }
                                            } else{
                                                Console.WriteLine("Decryption done for video");
                                                if (!options.Nocleanup){
                                                    try{
                                                        if (File.Exists($"{tempTsFile}.video.enc.m4s")){
                                                            File.Delete($"{tempTsFile}.video.enc.m4s");
                                                        }

                                                        if (File.Exists($"{tempTsFile}.video.enc.m4s.resume")){
                                                            File.Delete($"{tempTsFile}.video.enc.m4s.resume");
                                                        }
                                                    } catch (Exception ex){
                                                        Console.WriteLine($"Failed to delete file {tempTsFile}.video.enc.m4s. Error: {ex.Message}");
                                                        // Handle exceptions if you need to log them or throw
                                                    }
                                                }

                                                try{
                                                    if (File.Exists($"{tsFile}.video.m4s")){
                                                        File.Delete($"{tsFile}.video.m4s");
                                                    }

                                                    File.Move($"{tempTsFile}.video.m4s", $"{tsFile}.video.m4s");
                                                } catch (IOException ex){
                                                    Console.WriteLine($"An error occurred: {ex.Message}");
                                                }

                                                files.Add(new DownloadedMedia{
                                                    Type = DownloadMediaType.Video,
                                                    Path = $"{tsFile}.video.m4s",
                                                    Lang = lang.Value,
                                                    IsPrimary = isPrimary
                                                });
                                            }
                                        } else{
                                            Console.WriteLine("No Video downloaded");
                                        }

                                        if (audioDownloaded){
                                            Console.WriteLine("Started decrypting audio");
                                            data.DownloadProgress = new DownloadProgress(){
                                                IsDownloading = true,
                                                Percent = 100,
                                                Time = 0,
                                                DownloadSpeed = 0,
                                                Doing = "Decrypting audio"
                                            };
                                            Queue.Refresh();
                                            var decryptAudio = await Helpers.ExecuteCommandAsync("mp4decrypt", CfgManager.PathMP4Decrypt, commandAudio);

                                            if (!decryptAudio.IsOk){
                                                Console.Error.WriteLine($"Decryption failed with exit code {decryptAudio.ErrorCode}");
                                                try{
                                                    File.Move($"{tempTsFile}.audio.enc.m4s", $"{tsFile}.audio.enc.m4s");
                                                } catch (IOException ex){
                                                    Console.WriteLine($"An error occurred: {ex.Message}");
                                                }
                                            } else{
                                                Console.WriteLine("Decryption done for audio");
                                                if (!options.Nocleanup){
                                                    try{
                                                        if (File.Exists($"{tempTsFile}.audio.enc.m4s")){
                                                            File.Delete($"{tempTsFile}.audio.enc.m4s");
                                                        }

                                                        if (File.Exists($"{tempTsFile}.audio.enc.m4s.resume")){
                                                            File.Delete($"{tempTsFile}.audio.enc.m4s.resume");
                                                        }
                                                    } catch (Exception ex){
                                                        Console.WriteLine($"Failed to delete file {tempTsFile}.audio.enc.m4s. Error: {ex.Message}");
                                                        // Handle exceptions if you need to log them or throw
                                                    }
                                                }

                                                try{
                                                    if (File.Exists($"{tsFile}.audio.m4s")){
                                                        File.Delete($"{tsFile}.audio.m4s");
                                                    }

                                                    File.Move($"{tempTsFile}.audio.m4s", $"{tsFile}.audio.m4s");
                                                } catch (IOException ex){
                                                    Console.WriteLine($"An error occurred: {ex.Message}");
                                                }

                                                files.Add(new DownloadedMedia{
                                                    Type = DownloadMediaType.Audio,
                                                    Path = $"{tsFile}.audio.m4s",
                                                    Lang = lang.Value,
                                                    IsPrimary = isPrimary
                                                });
                                            }
                                        } else{
                                            Console.WriteLine("No Audio downloaded");
                                        }
                                    } else{
                                        Console.Error.WriteLine("mp4decrypt not found, files need decryption. Decryption Keys: ");
                                        MainWindow.Instance.ShowError($"mp4decrypt not found, files need decryption");
                                    }
                                } else{
                                    if (videoDownloaded){
                                        files.Add(new DownloadedMedia{
                                            Type = DownloadMediaType.Video,
                                            Path = $"{tsFile}.video.m4s",
                                            Lang = lang.Value,
                                            IsPrimary = isPrimary
                                        });
                                    }

                                    if (audioDownloaded){
                                        files.Add(new DownloadedMedia{
                                            Type = DownloadMediaType.Audio,
                                            Path = $"{tsFile}.audio.m4s",
                                            Lang = lang.Value,
                                            IsPrimary = isPrimary
                                        });
                                    }
                                }
                            } else if (options.Novids){
                                fileName = Path.Combine(FileNameManager.ParseFileName(options.FileName, variables, options.Numbers, options.Override).ToArray());
                                Console.WriteLine("Downloading skipped!");
                            }
                        }
                    } else if (options.Novids && options.Noaudio){
                        fileName = Path.Combine(FileNameManager.ParseFileName(options.FileName, variables, options.Numbers, options.Override).ToArray());
                    }

                    if (compiledChapters.Count > 0){
                        try{
                            // Parsing and constructing the file names
                            fileName = Path.Combine(FileNameManager.ParseFileName(options.FileName, variables, options.Numbers, options.Override).ToArray());
                            string outFile = Path.Combine(FileNameManager.ParseFileName(options.FileName + "." + (epMeta.Lang?.Name), variables, options.Numbers, options.Override).ToArray());
                            if (Path.IsPathRooted(outFile)){
                                tsFile = outFile;
                            } else{
                                tsFile = Path.Combine(fileDir, outFile);
                            }

                            // Check if the path is absolute
                            bool isAbsolute = Path.IsPathRooted(outFile);

                            // Get all directory parts of the path except the last segment (assuming it's a file)
                            string[] directories = Path.GetDirectoryName(outFile)?.Split(Path.DirectorySeparatorChar) ?? Array.Empty<string>();

                            // Initialize the cumulative path based on whether the original path is absolute or not
                            string cumulativePath = isAbsolute ? "" : fileDir;
                            for (int i = 0; i < directories.Length; i++){
                                // Build the path incrementally
                                cumulativePath = Path.Combine(cumulativePath, directories[i]);

                                // Check if the directory exists and create it if it does not
                                if (!Directory.Exists(cumulativePath)){
                                    Directory.CreateDirectory(cumulativePath);
                                    Console.WriteLine($"Created directory: {cumulativePath}");
                                }
                            }

                            // Finding language by code
                            var lang = Languages.languages.FirstOrDefault(l => l.Code == curStream?.AudioLang);
                            if (lang.Code == "und"){
                                Console.Error.WriteLine($"Unable to find language for code {curStream?.AudioLang}");
                            }

                            File.WriteAllText($"{tsFile}.txt", string.Join("\r\n", compiledChapters));

                            files.Add(new DownloadedMedia{ Path = $"{tsFile}.txt", Lang = lang, Type = DownloadMediaType.Chapters });
                        } catch{
                            Console.Error.WriteLine("Failed to write chapter file");
                        }
                    }

                    if (options.DlSubs.IndexOf("all") > -1){
                        options.DlSubs = new List<string>{ "all" };
                    }

                    if (options.Hslang != "none"){
                        Console.WriteLine("Subtitles downloading disabled for hardsubed streams.");
                        options.SkipSubs = true;
                    }

                    if (!options.SkipSubs && options.DlSubs.IndexOf("none") == -1){
                        await DownloadSubtitles(options, pbData, audDub, fileName, files, fileDir);
                    } else{
                        Console.WriteLine("Subtitles downloading skipped!");
                    }
                }

                await Task.Delay(options.Waittime);
            }
        }

        // variables.Add(new Variable("height", quality == 0 ? plQuality.Last().RESOLUTION.Height : plQuality[quality - 1].RESOLUTION.Height, false));
        // variables.Add(new Variable("width", quality == 0 ? plQuality.Last().RESOLUTION.Width : plQuality[quality - 1].RESOLUTION.Width, false));

        if (options.IncludeVideoDescription){
            string fullPath = (Path.IsPathRooted(fileName) ? fileName : Path.Combine(fileDir, fileName)) + ".xml";

            if (!File.Exists(fullPath)){
                using (XmlWriter writer = XmlWriter.Create(fullPath)){
                    writer.WriteStartDocument();
                    writer.WriteStartElement("Tags");

                    writer.WriteStartElement("Tag");

                    writer.WriteStartElement("Targets");
                    writer.WriteElementString("TargetTypeValue", "50");
                    writer.WriteEndElement(); // End Targets

                    writer.WriteStartElement("Simple");
                    writer.WriteElementString("Name", "DESCRIPTION");
                    writer.WriteElementString("String", data.Description);
                    writer.WriteEndElement(); // End Simple

                    writer.WriteEndElement(); // End Tag

                    writer.WriteEndElement(); // End Tags
                    writer.WriteEndDocument();
                }
            }

            Console.WriteLine($"{fileName} has been created with the description.");
        }


        return new DownloadResponse{
            Data = files,
            Error = dlFailed,
            FileName = fileName.Length > 0 ? (Path.IsPathRooted(fileName) ? fileName : Path.Combine(fileDir, fileName)) : "./unknown",
            ErrorText = "",
            VideoTitle = FileNameManager.ParseFileName(options.VideoTitle, variables, options.Numbers, options.Override).Last()
        };
    }

    private static async Task DownloadSubtitles(CrDownloadOptions options, PlaybackData pbData, string audDub, string fileName, List<DownloadedMedia> files, string fileDir){
        if (pbData.Meta != null && pbData.Meta.Subtitles != null && pbData.Meta.Subtitles.Count > 0){
            List<SubtitleInfo> subsData = pbData.Meta.Subtitles.Values.ToList();
            List<SubtitleInfo> capsData = pbData.Meta.ClosedCaptions?.Values.ToList() ?? new List<SubtitleInfo>();
            var subsDataMapped = subsData.Select(s => {
                var subLang = Languages.FixAndFindCrLc((s.Locale ?? Locale.DefaulT).GetEnumMemberValue());
                return new{
                    format = s.Format,
                    url = s.Url,
                    locale = subLang,
                    language = subLang.Locale,
                    isCC = false
                };
            }).ToList();

            var capsDataMapped = capsData.Select(s => {
                var subLang = Languages.FixAndFindCrLc((s.Locale ?? Locale.DefaulT).GetEnumMemberValue());
                return new{
                    format = s.Format,
                    url = s.Url,
                    locale = subLang,
                    language = subLang.Locale,
                    isCC = true
                };
            }).ToList();

            subsDataMapped.AddRange(capsDataMapped);

            var subsArr = Languages.SortSubtitles(subsDataMapped, "language");

            foreach (var subsItem in subsArr){
                var index = subsArr.IndexOf(subsItem);
                var langItem = subsItem.locale;
                var sxData = new SxItem();
                sxData.Language = langItem;
                var isSigns = langItem.Code == audDub && !subsItem.isCC;
                var isCc = subsItem.isCC;

                sxData.File = Languages.SubsFile(fileName, index + "", langItem, isCc, options.CcTag, isSigns, subsItem.format, !(options.DlSubs.Count == 1 && !options.DlSubs.Contains("all")));
                sxData.Path = Path.Combine(fileDir, sxData.File);

                Helpers.EnsureDirectoriesExist(sxData.Path);

                // Check if any file matches the specified conditions
                if (files.Any(a => a.Type == DownloadMediaType.Subtitle &&
                                   (a.Language.CrLocale == langItem.CrLocale || a.Language.Locale == langItem.Locale) &&
                                   a.Cc == isCc &&
                                   a.Signs == isSigns) || (!options.IncludeSignsSubs && isSigns)){
                    continue;
                }

                if (options.DlSubs.Contains("all") || options.DlSubs.Contains(langItem.CrLocale)){
                    var subsAssReq = HttpClientReq.CreateRequestMessage(subsItem.url ?? string.Empty, HttpMethod.Get, false, false, null);

                    var subsAssReqResponse = await HttpClientReq.Instance.SendHttpRequest(subsAssReq);

                    if (subsAssReqResponse.IsOk){
                        if (subsItem.format == "ass"){
                            subsAssReqResponse.ResponseContent = '\ufeff' + subsAssReqResponse.ResponseContent;
                            var sBodySplit = subsAssReqResponse.ResponseContent.Split(new[]{ "\r\n" }, StringSplitOptions.None).ToList();
                            // Insert 'ScaledBorderAndShadow' after the second line
                            if (sBodySplit.Count > 2){
                                if (options.SubsAddScaledBorder == ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes){
                                    sBodySplit.Insert(2, "ScaledBorderAndShadow: yes");
                                } else if (options.SubsAddScaledBorder == ScaledBorderAndShadowSelection.ScaledBorderAndShadowNo){
                                    sBodySplit.Insert(2, "ScaledBorderAndShadow: no");
                                }
                            }


                            // Rejoin the lines back into a single string
                            subsAssReqResponse.ResponseContent = string.Join("\r\n", sBodySplit);

                            // Extract the title from the second line and remove 'Title: ' prefix
                            if (sBodySplit.Count > 1){
                                sxData.Title = sBodySplit[1].Replace("Title: ", "");
                                sxData.Title = $"{langItem.Language} / {sxData.Title}";
                                var keysList = FontsManager.ExtractFontsFromAss(subsAssReqResponse.ResponseContent);
                                sxData.Fonts = FontsManager.Instance.GetDictFromKeyList(keysList);
                            }
                        } else if (subsItem.format == "vtt"){
                            // TODO
                        }

                        File.WriteAllText(sxData.Path, subsAssReqResponse.ResponseContent);
                        Console.WriteLine($"Subtitle downloaded: ${sxData.File}");
                        files.Add(new DownloadedMedia{
                            Type = DownloadMediaType.Subtitle,
                            Cc = isCc,
                            Signs = isSigns,
                            Path = sxData.Path,
                            File = sxData.File,
                            Title = sxData.Title,
                            Fonts = sxData.Fonts,
                            Language = sxData.Language,
                            Lang = sxData.Language
                        });
                    } else{
                        Console.WriteLine($"Failed to download subtitle: ${sxData.File}");
                    }
                }
            }
        } else{
            Console.WriteLine("Can\'t find urls for subtitles!");
        }
    }

    private async Task<(bool Ok, PartsData Parts, string tsFile)> DownloadVideo(VideoItem chosenVideoSegments, CrDownloadOptions options, string outFile, string tsFile, string tempTsFile, CrunchyEpMeta data,
        string fileDir){
        // Prepare for video download
        int totalParts = chosenVideoSegments.segments.Count;
        int mathParts = (int)Math.Ceiling((double)totalParts / options.Partsize);
        string mathMsg = $"({mathParts}*{options.Partsize})";
        Console.WriteLine($"Total parts in video stream: {totalParts} {mathMsg}");

        if (Path.IsPathRooted(outFile)){
            tsFile = outFile;
        } else{
            tsFile = Path.Combine(fileDir, outFile);
        }

        Helpers.EnsureDirectoriesExist(tsFile);

        M3U8Json videoJson = new M3U8Json{
            Segments = chosenVideoSegments.segments.Cast<dynamic>().ToList()
        };

        var videoDownloader = new HlsDownloader(new HlsOptions{
            Output = chosenVideoSegments.pssh != null ? $"{tempTsFile}.video.enc.m4s" : $"{tsFile}.video.m4s",
            Timeout = options.Timeout,
            M3U8Json = videoJson,
            // BaseUrl = chunkPlaylist.BaseUrl, 
            Threads = options.Partsize,
            FsRetryTime = options.FsRetryTime * 1000,
            Override = options.Force,
        }, data, true, false);

        var videoDownloadResult = await videoDownloader.Download();

        return (videoDownloadResult.Ok, videoDownloadResult.Parts, tsFile);
    }

    private async Task<(bool Ok, PartsData Parts, string tsFile)> DownloadAudio(AudioItem chosenAudioSegments, CrDownloadOptions options, string outFile, string tsFile, string tempTsFile, CrunchyEpMeta data,
        string fileDir){
        // Prepare for audio download
        int totalParts = chosenAudioSegments.segments.Count;
        int mathParts = (int)Math.Ceiling((double)totalParts / options.Partsize);
        string mathMsg = $"({mathParts}*{options.Partsize})";
        Console.WriteLine($"Total parts in audio stream: {totalParts} {mathMsg}");

        if (Path.IsPathRooted(outFile)){
            tsFile = outFile;
        } else{
            tsFile = Path.Combine(fileDir, outFile);
        }

        // Check if the path is absolute
        bool isAbsolute = Path.IsPathRooted(outFile);

        // Get all directory parts of the path except the last segment (assuming it's a file)
        string[] directories = Path.GetDirectoryName(outFile)?.Split(Path.DirectorySeparatorChar) ?? Array.Empty<string>();

        // Initialize the cumulative path based on whether the original path is absolute or not
        string cumulativePath = isAbsolute ? "" : fileDir;
        for (int i = 0; i < directories.Length; i++){
            // Build the path incrementally
            cumulativePath = Path.Combine(cumulativePath, directories[i]);

            // Check if the directory exists and create it if it does not
            if (!Directory.Exists(cumulativePath)){
                Directory.CreateDirectory(cumulativePath);
                Console.WriteLine($"Created directory: {cumulativePath}");
            }
        }

        M3U8Json audioJson = new M3U8Json{
            Segments = chosenAudioSegments.segments.Cast<dynamic>().ToList()
        };

        var audioDownloader = new HlsDownloader(new HlsOptions{
            Output = chosenAudioSegments.pssh != null ? $"{tempTsFile}.audio.enc.m4s" : $"{tsFile}.audio.m4s",
            Timeout = options.Timeout,
            M3U8Json = audioJson,
            // BaseUrl = chunkPlaylist.BaseUrl, 
            Threads = options.Partsize,
            FsRetryTime = options.FsRetryTime * 1000,
            Override = options.Force,
        }, data, false, true);

        var audioDownloadResult = await audioDownloader.Download();


        return (audioDownloadResult.Ok, audioDownloadResult.Parts, tsFile);
    }

    private async Task<(bool IsOk, PlaybackData pbData, string error)> FetchPlaybackData(string mediaId, string mediaGuidId, CrunchyEpMetaData epMeta){
        PlaybackData temppbData = new PlaybackData{ Total = 0, Data = new List<Dictionary<string, Dictionary<string, StreamDetails>>>() };
        bool ok = true;

        HttpRequestMessage playbackRequest;
        (bool IsOk, string ResponseContent) playbackRequestResponse;


        playbackRequest = HttpClientReq.CreateRequestMessage($"https://cr-play-service.prd.crunchyrollsvc.com/v1/{mediaGuidId}/{CrunOptions.StreamEndpoint}/play", HttpMethod.Get, true, false, null);

        playbackRequestResponse = await HttpClientReq.Instance.SendHttpRequest(playbackRequest);

        if (!playbackRequestResponse.IsOk && playbackRequestResponse.ResponseContent != string.Empty){
            var s = playbackRequestResponse.ResponseContent;
            var error = StreamError.FromJson(s);
            if (error != null && error.IsTooManyActiveStreamsError()){
                foreach (var errorActiveStream in error.ActiveStreams){
                    await HttpClientReq.DeAuthVideo(errorActiveStream.ContentId, errorActiveStream.Token);
                }

                playbackRequest = HttpClientReq.CreateRequestMessage($"https://cr-play-service.prd.crunchyrollsvc.com/v1/{mediaGuidId}/{CrunOptions.StreamEndpoint}/play", HttpMethod.Get, true, false, null);
                playbackRequestResponse = await HttpClientReq.Instance.SendHttpRequest(playbackRequest);
            }
        }

        if (playbackRequestResponse.IsOk){
            temppbData = new PlaybackData{ Total = 0, Data = new List<Dictionary<string, Dictionary<string, StreamDetails>>>() };
            temppbData.Data.Add(new Dictionary<string, Dictionary<string, StreamDetails>>());

            CrunchyStreamData? playStream = JsonConvert.DeserializeObject<CrunchyStreamData>(playbackRequestResponse.ResponseContent, SettingsJsonSerializerSettings);
            CrunchyStreams derivedPlayCrunchyStreams = new CrunchyStreams();
            if (playStream != null){
                if (playStream.Token != null) await HttpClientReq.DeAuthVideo(mediaGuidId, playStream.Token);

                if (playStream.HardSubs != null)
                    foreach (var hardsub in playStream.HardSubs){
                        var stream = hardsub.Value;
                        derivedPlayCrunchyStreams[hardsub.Key] = new StreamDetails{
                            Url = stream.Url,
                            HardsubLocale = stream.Hlang
                        };
                    }

                derivedPlayCrunchyStreams[""] = new StreamDetails{
                    Url = playStream.Url,
                    HardsubLocale = Locale.DefaulT
                };

                if (temppbData.Data != null){
                    temppbData.Data[0]["drm_adaptive_dash"] = derivedPlayCrunchyStreams;
                    temppbData.Total = 1;
                }

                temppbData.Meta = new PlaybackMeta(){ AudioLocale = playStream.AudioLocale, Versions = playStream.Versions, Bifs = new List<string>{ playStream.Bifs }, MediaId = mediaId };

                if (playStream.Captions != null){
                    temppbData.Meta.Captions = playStream.Captions;
                }

                temppbData.Meta.Subtitles = new Subtitles();
                foreach (var playStreamSubtitle in playStream.Subtitles){
                    Subtitle sub = playStreamSubtitle.Value;
                    temppbData.Meta.Subtitles.Add(playStreamSubtitle.Key, new SubtitleInfo(){ Format = sub.Format, Locale = sub.Locale, Url = sub.Url });
                }
            }
        } else{
            Console.WriteLine("Request Stream URLs FAILED! Attempting fallback");

            playbackRequest = HttpClientReq.CreateRequestMessage($"https://cr-play-service.prd.crunchyrollsvc.com/v1/{mediaGuidId}/web/firefox/play", HttpMethod.Get, true, false, null);

            playbackRequestResponse = await HttpClientReq.Instance.SendHttpRequest(playbackRequest);

            if (!playbackRequestResponse.IsOk && playbackRequestResponse.ResponseContent != string.Empty){
                var s = playbackRequestResponse.ResponseContent;
                var error = StreamError.FromJson(s);
                if (error != null && error.IsTooManyActiveStreamsError()){
                    foreach (var errorActiveStream in error.ActiveStreams){
                        await HttpClientReq.DeAuthVideo(errorActiveStream.ContentId, errorActiveStream.Token);
                    }

                    playbackRequest = HttpClientReq.CreateRequestMessage($"https://cr-play-service.prd.crunchyrollsvc.com/v1/{mediaGuidId}/web/firefox/play", HttpMethod.Get, true, false, null);
                    playbackRequestResponse = await HttpClientReq.Instance.SendHttpRequest(playbackRequest);
                }
            }

            if (playbackRequestResponse.IsOk){
                temppbData = new PlaybackData{ Total = 0, Data = new List<Dictionary<string, Dictionary<string, StreamDetails>>>() };
                temppbData.Data.Add(new Dictionary<string, Dictionary<string, StreamDetails>>());

                CrunchyStreamData? playStream = JsonConvert.DeserializeObject<CrunchyStreamData>(playbackRequestResponse.ResponseContent, SettingsJsonSerializerSettings);
                CrunchyStreams derivedPlayCrunchyStreams = new CrunchyStreams();
                if (playStream != null){
                    if (playStream.Token != null) await HttpClientReq.DeAuthVideo(mediaGuidId, playStream.Token);

                    if (playStream.HardSubs != null)
                        foreach (var hardsub in playStream.HardSubs){
                            var stream = hardsub.Value;
                            derivedPlayCrunchyStreams[hardsub.Key] = new StreamDetails{
                                Url = stream.Url,
                                HardsubLocale = stream.Hlang
                            };
                        }

                    derivedPlayCrunchyStreams[""] = new StreamDetails{
                        Url = playStream.Url,
                        HardsubLocale = Locale.DefaulT
                    };

                    if (temppbData.Data != null){
                        temppbData.Data[0]["drm_adaptive_dash"] = derivedPlayCrunchyStreams;
                        temppbData.Total = 1;
                    }

                    temppbData.Meta = new PlaybackMeta(){ AudioLocale = playStream.AudioLocale, Versions = playStream.Versions, Bifs = new List<string>{ playStream.Bifs }, MediaId = mediaId };

                    if (playStream.Captions != null){
                        temppbData.Meta.Captions = playStream.Captions;
                    }

                    temppbData.Meta.Subtitles = new Subtitles();
                    foreach (var playStreamSubtitle in playStream.Subtitles){
                        Subtitle sub = playStreamSubtitle.Value;
                        temppbData.Meta.Subtitles.Add(playStreamSubtitle.Key, new SubtitleInfo(){ Format = sub.Format, Locale = sub.Locale, Url = sub.Url });
                    }
                }
            } else{
                Console.Error.WriteLine("'Fallback Request Stream URLs FAILED!'");
                ok = playbackRequestResponse.IsOk;
            }
        }


        return (IsOk: ok, pbData: temppbData, error: ok ? "" : playbackRequestResponse.ResponseContent);
    }

    private async Task ParseChapters(string currentMediaId, List<string> compiledChapters){
        var showRequest = HttpClientReq.CreateRequestMessage($"https://static.crunchyroll.com/skip-events/production/{currentMediaId}.json", HttpMethod.Get, true, true, null);

        var showRequestResponse = await HttpClientReq.Instance.SendHttpRequest(showRequest);

        if (showRequestResponse.IsOk){
            JObject jObject = JObject.Parse(showRequestResponse.ResponseContent);

            CrunchyChapters chapterData = new CrunchyChapters();
            chapterData.lastUpdate = jObject["lastUpdate"]?.ToObject<DateTime?>();
            chapterData.mediaId = jObject["mediaId"]?.ToObject<string>();
            chapterData.Chapters = new List<CrunchyChapter>();

            foreach (var property in jObject.Properties()){
                // Check if the property value is an object and the property is not one of the known non-dictionary properties
                if (property.Value.Type == JTokenType.Object && property.Name != "lastUpdate" && property.Name != "mediaId"){
                    // Deserialize the property value into a CrunchyChapter and add it to the dictionary
                    CrunchyChapter chapter = property.Value.ToObject<CrunchyChapter>();
                    chapterData.Chapters.Add(chapter);
                }
            }

            if (chapterData.Chapters.Count > 0){
                chapterData.Chapters.Sort((a, b) => {
                    if (a.start != null && b.start != null)
                        return a.start.Value - b.start.Value;
                    return 0;
                });

                if (!((chapterData.Chapters.Any(c => c.type == "intro")) || chapterData.Chapters.Any(c => c.type == "recap"))){
                    int chapterNumber = (compiledChapters.Count / 2) + 1;
                    compiledChapters.Add($"CHAPTER{chapterNumber}=00:00:00.00");
                    compiledChapters.Add($"CHAPTER{chapterNumber}NAME=Episode");
                }

                foreach (CrunchyChapter chapter in chapterData.Chapters){
                    if (chapter.start == null || chapter.end == null) continue;

                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                    DateTime startTime = epoch.AddSeconds(chapter.start.Value);
                    DateTime endTime = epoch.AddSeconds(chapter.end.Value);

                    string startFormatted = startTime.ToString("HH:mm:ss") + ".00";
                    string endFormatted = endTime.ToString("HH:mm:ss") + ".00";

                    int chapterNumber = (compiledChapters.Count / 2) + 1;
                    if (chapter.type == "intro"){
                        if (chapter.start > 0){
                            compiledChapters.Add($"CHAPTER{chapterNumber}=00:00:00.00");
                            compiledChapters.Add($"CHAPTER{chapterNumber}NAME=Prologue");
                        }

                        chapterNumber = (compiledChapters.Count / 2) + 1;
                        compiledChapters.Add($"CHAPTER{chapterNumber}={startFormatted}");
                        compiledChapters.Add($"CHAPTER{chapterNumber}NAME=Opening");
                        chapterNumber = (compiledChapters.Count / 2) + 1;
                        compiledChapters.Add($"CHAPTER{chapterNumber}={endFormatted}");
                        compiledChapters.Add($"CHAPTER{chapterNumber}NAME=Episode");
                    } else{
                        string formattedChapterType = char.ToUpper(chapter.type[0]) + chapter.type.Substring(1);
                        chapterNumber = (compiledChapters.Count / 2) + 1;
                        compiledChapters.Add($"CHAPTER{chapterNumber}={startFormatted}");
                        compiledChapters.Add($"CHAPTER{chapterNumber}NAME={formattedChapterType} Start");
                        chapterNumber = (compiledChapters.Count / 2) + 1;
                        compiledChapters.Add($"CHAPTER{chapterNumber}={endFormatted}");
                        compiledChapters.Add($"CHAPTER{chapterNumber}NAME={formattedChapterType} End");
                    }
                }
            }
        } else{
            Console.WriteLine("Chapter request failed, attempting old API ");

            showRequest = HttpClientReq.CreateRequestMessage($"https://static.crunchyroll.com/datalab-intro-v2/{currentMediaId}.json", HttpMethod.Get, true, true, null);

            showRequestResponse = await HttpClientReq.Instance.SendHttpRequest(showRequest);

            if (showRequestResponse.IsOk){
                CrunchyOldChapter chapterData = JsonConvert.DeserializeObject<CrunchyOldChapter>(showRequestResponse.ResponseContent);

                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                DateTime startTime = epoch.AddSeconds(chapterData.startTime);
                DateTime endTime = epoch.AddSeconds(chapterData.endTime);

                string[] startTimeParts = startTime.ToString(CultureInfo.CurrentCulture).Split('.');
                string[] endTimeParts = endTime.ToString(CultureInfo.CurrentCulture).Split('.');

                string startMs = startTimeParts.Length > 1 ? startTimeParts[1] : "00";
                string endMs = endTimeParts.Length > 1 ? endTimeParts[1] : "00";

                string startFormatted = startTime.ToString("HH:mm:ss") + "." + startMs;
                string endFormatted = endTime.ToString("HH:mm:ss") + "." + endMs;

                int chapterNumber = (compiledChapters.Count / 2) + 1;
                if (chapterData.startTime > 1){
                    compiledChapters.Add($"CHAPTER{chapterNumber}=00:00:00.00");
                    compiledChapters.Add($"CHAPTER{chapterNumber}NAME=Prologue");
                }

                chapterNumber = (compiledChapters.Count / 2) + 1;
                compiledChapters.Add($"CHAPTER{chapterNumber}={startFormatted}");
                compiledChapters.Add($"CHAPTER{chapterNumber}NAME=Opening");
                chapterNumber = (compiledChapters.Count / 2) + 1;
                compiledChapters.Add($"CHAPTER{chapterNumber}={endFormatted}");
                compiledChapters.Add($"CHAPTER{chapterNumber}NAME=Episode");
            } else{
                Console.WriteLine("Old Chapter API request failed");
            }
        }
    }
}