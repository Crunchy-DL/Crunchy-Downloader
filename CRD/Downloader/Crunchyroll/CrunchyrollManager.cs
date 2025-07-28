using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CRD.Utils;
using CRD.Utils.DRM;
using CRD.Utils.Ffmpeg_Encoding;
using CRD.Utils.Files;
using CRD.Utils.HLS;
using CRD.Utils.Muxing;
using CRD.Utils.Sonarr;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll;
using CRD.Utils.Structs.History;
using CRD.ViewModels;
using CRD.ViewModels.Utils;
using CRD.Views;
using CRD.Views.Utils;
using FluentAvalonia.UI.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LanguageItem = CRD.Utils.Structs.LanguageItem;

namespace CRD.Downloader.Crunchyroll;

public class CrunchyrollManager{
    public CrToken? Token;

    public CrProfile Profile = new();
    private readonly Lazy<CrDownloadOptions> _optionsLazy;
    public CrDownloadOptions CrunOptions => _optionsLazy.Value;

    #region History Variables

    public ObservableCollection<HistorySeries> HistoryList = new();

    public HistorySeries SelectedSeries = new HistorySeries{
        Seasons =[]
    };

    #endregion

    public CrBrowseSeriesBase? AllCRSeries;


    public string DefaultLocale = "en-US";

    public JsonSerializerSettings? SettingsJsonSerializerSettings = new(){
        NullValueHandling = NullValueHandling.Ignore,
    };

    private Widevine _widevine = Widevine.Instance;

    public CrAuth CrAuth;
    public CrEpisode CrEpisode;
    public CrSeries CrSeries;
    public CrMovies CrMovies;
    public CrMusic CrMusic;
    public History History;

    #region Singelton

    private static CrunchyrollManager? _instance;
    private static readonly object Padlock = new();

    public static CrunchyrollManager Instance{
        get{
            if (_instance == null){
                lock (Padlock){
                    if (_instance == null){
                        _instance = new CrunchyrollManager();
                    }
                }
            }

            return _instance;
        }
    }

    #endregion

    public CrunchyrollManager(){
        _optionsLazy = new Lazy<CrDownloadOptions>(InitDownloadOptions, LazyThreadSafetyMode.ExecutionAndPublication);
    }


    private CrDownloadOptions InitDownloadOptions(){
        var options = new CrDownloadOptions();

        options.UseCrBetaApi = true;
        options.AutoDownload = false;
        options.RemoveFinishedDownload = false;
        options.Chapters = true;
        options.Hslang = "none";
        options.Force = "Y";
        options.FileName = "${seriesTitle} - S${season}E${episode} [${height}p]";
        options.Partsize = 10;
        options.DlSubs = new List<string>{ "en-US" };
        options.SkipMuxing = false;
        options.MkvmergeOptions =[];
        options.FfmpegOptions =[];
        options.DefaultAudio = "ja-JP";
        options.DefaultSub = "en-US";
        options.QualityAudio = "best";
        options.QualityVideo = "best";
        options.CcTag = "CC";
        options.CcSubsFont = "Trebuchet MS";
        options.RetryDelay = 5;
        options.RetryAttempts = 5;
        options.Numbers = 2;
        options.Timeout = 15000;
        options.DubLang = new List<string>(){ "ja-JP" };
        options.SimultaneousDownloads = 2;
        // options.AccentColor = Colors.SlateBlue.ToString();
        options.Theme = "System";
        options.SelectedCalendarLanguage = "en-us";
        options.CalendarDubFilter = "none";
        options.CustomCalendar = true;
        options.DlVideoOnce = true;
        options.StreamEndpoint = "web/firefox";
        options.SubsAddScaledBorder = ScaledBorderAndShadowSelection.DontAdd;
        options.HistoryLang = DefaultLocale;

        options.BackgroundImageOpacity = 0.5;
        options.BackgroundImageBlurRadius = 10;

        options.HistoryPageProperties = new HistoryPageProperties{
            SelectedView = HistoryViewType.Posters,
            SelectedSorting = SortingType.SeriesTitle,
            SelectedFilter = FilterType.All,
            ScaleValue = 0.73,
            Ascending = false,
            ShowSeries = true,
            ShowArtists = true
        };

        options.History = true;

        CfgManager.UpdateSettingsFromFile(options, CfgManager.PathCrDownloadOptions);

        return options;
    }

    public void InitOptions(){
        _widevine = Widevine.Instance;

        CrAuth = new CrAuth();
        CrEpisode = new CrEpisode();
        CrSeries = new CrSeries();
        CrMovies = new CrMovies();
        CrMusic = new CrMusic();
        History = new History();

        Profile = new CrProfile{
            Username = "???",
            Avatar = "crbrand_avatars_logo_marks_mangagirl_taupe.png",
            PreferredContentAudioLanguage = "ja-JP",
            PreferredContentSubtitleLanguage = DefaultLocale,
            HasPremium = false,
        };
    }

    public static async Task<string> GetBase64EncodedTokenAsync(){
        string url = "https://static.crunchyroll.com/vilos-v2/web/vilos/js/bundle.js";

        try{
            string jsContent = await HttpClientReq.Instance.GetHttpClient().GetStringAsync(url);

            Match match = Regex.Match(jsContent, @"prod=""([\w-]+:[\w-]+)""");

            if (!match.Success)
                throw new Exception("Token not found in JS file.");

            string token = match.Groups[1].Value;

            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            string base64Token = Convert.ToBase64String(tokenBytes);

            return base64Token;
        } catch (Exception ex){
            Console.Error.WriteLine($"Auth Token Fetch Error: {ex.Message}");
            return "";
        }
    }

    public async Task Init(){
        if (CrunOptions.LogMode){
            CfgManager.EnableLogMode();
        } else{
            CfgManager.DisableLogMode();
        }

        var token = await GetBase64EncodedTokenAsync();

        if (!string.IsNullOrEmpty(token)){
            ApiUrls.authBasicMob = "Basic " + token;
        }

        var jsonFiles = Directory.Exists(CfgManager.PathENCODING_PRESETS_DIR) ? Directory.GetFiles(CfgManager.PathENCODING_PRESETS_DIR, "*.json") :[];

        foreach (var file in jsonFiles){
            try{
                var jsonContent = File.ReadAllText(file);

                var obj = Helpers.Deserialize<VideoPreset>(jsonContent, null);

                if (obj != null){
                    FfmpegEncoding.AddPreset(obj);
                } else{
                    Console.Error.WriteLine("Failed to add Preset to Available Presets List");
                }
            } catch (Exception ex){
                Console.Error.WriteLine($"Failed to deserialize file {file}: {ex.Message}");
            }
        }

        if (CfgManager.CheckIfFileExists(CfgManager.PathCrToken)){
            Token = CfgManager.ReadJsonFromFile<CrToken>(CfgManager.PathCrToken);
            await CrAuth.LoginWithToken();
        } else{
            await CrAuth.AuthAnonymous();
        }


        if (CrunOptions.History){
            if (File.Exists(CfgManager.PathCrHistory)){
                var decompressedJson = CfgManager.DecompressJsonFile(CfgManager.PathCrHistory);

                if (!string.IsNullOrEmpty(decompressedJson)){
                    var historyList = Helpers.Deserialize<ObservableCollection<HistorySeries>>(
                        decompressedJson,
                        SettingsJsonSerializerSettings
                    );

                    if (historyList != null){
                        HistoryList = historyList;

                        Parallel.ForEach(historyList, historySeries => {
                            historySeries.Init();

                            foreach (var historySeriesSeason in historySeries.Seasons){
                                historySeriesSeason.Init();
                            }
                        });
                    } else{
                        HistoryList =[];
                    }
                } else{
                    HistoryList =[];
                }
            } else{
                HistoryList =[];
            }


            await SonarrClient.Instance.RefreshSonarr();
        }

        //Fix hslang - can be removed in a future version
        var lang = Languages.Locale2language(CrunOptions.Hslang);
        if (lang != Languages.DEFAULT_lang){
            CrunOptions.Hslang = lang.CrLocale;
        }
    }


    public async Task<bool> DownloadEpisode(CrunchyEpMeta data, CrDownloadOptions options){
        QueueManager.Instance.ActiveDownloads++;

        data.DownloadProgress = new DownloadProgress(){
            IsDownloading = true,
            Error = false,
            Percent = 0,
            Time = 0,
            DownloadSpeed = 0,
            Doing = "Starting"
        };
        QueueManager.Instance.Queue.Refresh();
        var res = await DownloadMediaList(data, options);

        if (res.Error){
            QueueManager.Instance.ActiveDownloads--;
            data.DownloadProgress = new DownloadProgress(){
                IsDownloading = false,
                Error = true,
                Percent = 100,
                Time = 0,
                DownloadSpeed = 0,
                Doing = "Download Error" + (!string.IsNullOrEmpty(res.ErrorText) ? " - " + res.ErrorText : ""),
            };
            QueueManager.Instance.Queue.Refresh();
            return false;
        }

        if (options.SkipMuxing == false){
            bool syncError = false;
            bool muxError = false;
            var notSyncedDubs = "";

            data.DownloadProgress = new DownloadProgress(){
                IsDownloading = true,
                Percent = 100,
                Time = 0,
                DownloadSpeed = 0,
                Doing = "Muxing"
            };

            QueueManager.Instance.Queue.Refresh();

            if (options.MuxFonts){
                await FontsManager.Instance.GetFontsAsync();
            }

            var fileNameAndPath = options.DownloadToTempFolder
                ? Path.Combine(res.TempFolderPath ?? string.Empty, res.FileName ?? string.Empty)
                : Path.Combine(res.FolderPath ?? string.Empty, res.FileName ?? string.Empty);
            if (options is{ DlVideoOnce: false, KeepDubsSeperate: true }){
                var groupByDub = Helpers.GroupByLanguageWithSubtitles(res.Data);
                var mergers = new List<Merger>();
                foreach (var keyValue in groupByDub){
                    var result = await MuxStreams(keyValue.Value,
                        new CrunchyMuxOptions{
                            DubLangList = options.DubLang,
                            SubLangList = options.DlSubs,
                            FfmpegOptions = options.FfmpegOptions,
                            SkipSubMux = options.SkipSubsMux,
                            Output = fileNameAndPath + $".{keyValue.Value.First().Lang.Locale}",
                            Mp4 = options.Mp4,
                            Mp3 = options.AudioOnlyToMp3,
                            MuxFonts = options.MuxFonts,
                            MuxCover = options.MuxCover,
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
                            MuxDescription = options.IncludeVideoDescription,
                            DlVideoOnce = options.DlVideoOnce,
                            DefaultSubSigns = options.DefaultSubSigns,
                            DefaultSubForcedDisplay = options.DefaultSubForcedDisplay,
                            CcSubsMuxingFlag = options.CcSubsMuxingFlag,
                            SignsSubsAsForced = options.SignsSubsAsForced,
                        },
                        fileNameAndPath + $".{keyValue.Value.First().Lang.Locale}", data);

                    if (result is{ merger: not null, isMuxed: true }){
                        mergers.Add(result.merger);
                    }

                    if (!result.isMuxed){
                        muxError = true;
                    }

                    if (result.syncError){
                        syncError = true;
                    }
                }

                foreach (var merger in mergers){
                    merger.CleanUp();

                    if (options.IsEncodeEnabled){
                        data.DownloadProgress = new DownloadProgress(){
                            IsDownloading = true,
                            Percent = 100,
                            Time = 0,
                            DownloadSpeed = 0,
                            Doing = "Encoding"
                        };

                        QueueManager.Instance.Queue.Refresh();

                        var preset = FfmpegEncoding.GetPreset(options.EncodingPresetName ?? string.Empty);

                        if (preset != null) await Helpers.RunFFmpegWithPresetAsync(merger.options.Output, preset, data);
                    }

                    if (options.DownloadToTempFolder){
                        await MoveFromTempFolder(merger, data, options, res.TempFolderPath ?? CfgManager.PathTEMP_DIR, res.Data.Where(e => e.Type == DownloadMediaType.Subtitle));
                    }
                }
            } else{
                var result = await MuxStreams(res.Data,
                    new CrunchyMuxOptions{
                        DubLangList = options.DubLang,
                        SubLangList = options.DlSubs,
                        FfmpegOptions = options.FfmpegOptions,
                        SkipSubMux = options.SkipSubsMux,
                        Output = fileNameAndPath,
                        Mp4 = options.Mp4,
                        Mp3 = options.AudioOnlyToMp3,
                        MuxFonts = options.MuxFonts,
                        MuxCover = options.MuxCover,
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
                        MuxDescription = options.IncludeVideoDescription,
                        DlVideoOnce = options.DlVideoOnce,
                        DefaultSubSigns = options.DefaultSubSigns,
                        DefaultSubForcedDisplay = options.DefaultSubForcedDisplay,
                        CcSubsMuxingFlag = options.CcSubsMuxingFlag,
                        SignsSubsAsForced = options.SignsSubsAsForced,
                    },
                    fileNameAndPath, data);

                syncError = result.syncError;
                notSyncedDubs = result.notSyncedDubs;
                muxError = !result.isMuxed;

                if (result is{ merger: not null, isMuxed: true }){
                    result.merger.CleanUp();
                }

                if (options.IsEncodeEnabled && !muxError){
                    data.DownloadProgress = new DownloadProgress(){
                        IsDownloading = true,
                        Percent = 100,
                        Time = 0,
                        DownloadSpeed = 0,
                        Doing = "Encoding"
                    };

                    QueueManager.Instance.Queue.Refresh();

                    var preset = FfmpegEncoding.GetPreset(options.EncodingPresetName ?? string.Empty);
                    if (preset != null && result.merger != null) await Helpers.RunFFmpegWithPresetAsync(result.merger.options.Output, preset, data);
                }

                if (options.DownloadToTempFolder){
                    await MoveFromTempFolder(result.merger, data, options, res.TempFolderPath ?? CfgManager.PathTEMP_DIR, res.Data.Where(e => e.Type == DownloadMediaType.Subtitle));
                }
            }


            data.DownloadProgress = new DownloadProgress(){
                IsDownloading = true,
                Done = true,
                Percent = 100,
                Time = 0,
                DownloadSpeed = 0,
                Doing = (muxError ? "Muxing Failed" : "Done") + (syncError ? $" - Couldn't sync dubs ({notSyncedDubs})" : "")
            };

            if (CrunOptions.RemoveFinishedDownload && !syncError){
                QueueManager.Instance.Queue.Remove(data);
            }
        } else{
            Console.WriteLine("Skipping mux");
            res.Data.ForEach(file => Helpers.DeleteFile(file.Path + ".resume"));
            if (options.DownloadToTempFolder){
                if (string.IsNullOrEmpty(res.TempFolderPath) || !Directory.Exists(res.TempFolderPath)){
                    Console.WriteLine("Invalid or non-existent temp folder path.");
                } else{
                    // Move files
                    foreach (var downloadedMedia in res.Data){
                        await MoveFile(downloadedMedia.Path ?? string.Empty, res.TempFolderPath, data.DownloadPath ?? CfgManager.PathVIDEOS_DIR, options);
                    }
                }
            }

            data.DownloadProgress = new DownloadProgress(){
                IsDownloading = true,
                Done = true,
                Percent = 100,
                Time = 0,
                DownloadSpeed = 0,
                Doing = "Done - Skipped muxing"
            };

            if (CrunOptions.RemoveFinishedDownload){
                QueueManager.Instance.Queue.Remove(data);
            }
        }


        QueueManager.Instance.ActiveDownloads--;
        QueueManager.Instance.Queue.Refresh();

        if (options.History && data.Data is{ Count: > 0 } && (options.HistoryIncludeCrArtists && data.Music || !data.Music)){
            var ids = data.Data.First().GetOriginalIds();
            History.SetAsDownloaded(data.SeriesId, ids.seasonID ?? data.SeasonId, ids.guid ?? data.Data.First().MediaId);
        }

        if (options.MarkAsWatched && data.Data is{ Count: > 0 }){
            _ = CrEpisode.MarkAsWatched(data.Data.First().MediaId);
        }

        if (QueueManager.Instance.Queue.Count == 0){
            try{
                var audioPath = CrunOptions.DownloadFinishedSoundPath;
                if (!string.IsNullOrEmpty(audioPath)){
                    var player = new AudioPlayer();
                    player.Play(audioPath);
                }
            } catch (Exception exception){
                Console.Error.WriteLine("Failed to play sound: " + exception);
            }

            if (CrunOptions.ShutdownWhenQueueEmpty){
                Helpers.ShutdownComputer();
            }
        }

        return true;
    }

    #region Temp Files Move

    private async Task MoveFromTempFolder(Merger? merger, CrunchyEpMeta data, CrDownloadOptions options, string tempFolderPath, IEnumerable<DownloadedMedia> subtitles){
        if (!options.DownloadToTempFolder) return;

        data.DownloadProgress = new DownloadProgress{
            IsDownloading = true,
            Percent = 100,
            Time = 0,
            DownloadSpeed = 0,
            Doing = "Moving Files"
        };

        QueueManager.Instance.Queue.Refresh();

        if (string.IsNullOrEmpty(tempFolderPath) || !Directory.Exists(tempFolderPath)){
            Console.WriteLine("Invalid or non-existent temp folder path.");
            return;
        }

        // Move the main output file
        await MoveFile(merger?.options.Output ?? string.Empty, tempFolderPath, data.DownloadPath ?? CfgManager.PathVIDEOS_DIR, options);

        // Move the subtitle files
        foreach (var downloadedMedia in subtitles){
            await MoveFile(downloadedMedia.Path ?? string.Empty, tempFolderPath, data.DownloadPath ?? CfgManager.PathVIDEOS_DIR, options);
        }
    }

    private async Task MoveFile(string sourcePath, string tempFolderPath, string downloadPath, CrDownloadOptions options){
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)){
            // Console.Error.WriteLine("Source file does not exist or path is invalid.");
            return;
        }

        if (!sourcePath.StartsWith(tempFolderPath)){
            Console.Error.WriteLine("Source file is not located in the temp folder.");
            return;
        }

        try{
            var fileName = sourcePath[tempFolderPath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var destinationFolder = !string.IsNullOrEmpty(downloadPath)
                ? downloadPath
                : !string.IsNullOrEmpty(options.DownloadDirPath)
                    ? options.DownloadDirPath
                    : CfgManager.PathVIDEOS_DIR;

            var destinationPath = Path.Combine(destinationFolder ?? string.Empty, fileName);

            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrEmpty(destinationDirectory)){
                Console.WriteLine("Invalid destination directory path.");
                return;
            }

            await Task.Run(() => {
                if (!Directory.Exists(destinationDirectory)){
                    Directory.CreateDirectory(destinationDirectory);
                }
            });

            await Task.Run(() => File.Move(sourcePath, destinationPath));
            Console.WriteLine($"File moved to {destinationPath}");
        } catch (IOException ex){
            Console.Error.WriteLine($"An error occurred while moving the file: {ex.Message}");
        } catch (UnauthorizedAccessException ex){
            Console.Error.WriteLine($"Access denied while moving the file: {ex.Message}");
        } catch (Exception ex){
            Console.Error.WriteLine($"An unexpected error occurred: {ex.Message}");
        }
    }

    #endregion

    private async Task<(Merger? merger, bool isMuxed, bool syncError, string notSyncedDubs)> MuxStreams(List<DownloadedMedia> data, CrunchyMuxOptions options, string filename, CrunchyEpMeta crunchyEpMeta){
        var muxToMp3 = false;

        if (options.Novids == true || data.FindAll(a => a.Type == DownloadMediaType.Video).Count == 0){
            if (data.FindAll(a => a.Type == DownloadMediaType.Audio).Count > 0){
                if (options.Mp3){
                    Console.WriteLine("Mux to MP3");
                    muxToMp3 = true;
                }
            } else{
                Console.WriteLine("Skip muxing since no videos are downloaded");
                return (null, false, false, "");
            }
        }

        var subs = data.Where(a => a.Type == DownloadMediaType.Subtitle).ToList();
        var subsList = new List<SubtitleFonts>();

        foreach (var downloadedMedia in subs){
            var subt = new SubtitleFonts();
            subt.Language = downloadedMedia.Language;
            subt.Fonts = downloadedMedia.Fonts ??[];
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
            var descriptionPath = data.First(a => a.Type == DownloadMediaType.Description).Path;
            if (File.Exists(descriptionPath)){
                muxDesc = true;
            } else{
                Console.Error.WriteLine("No xml description file found to mux description");
            }
        }


        var merger = new Merger(new MergerOptions{
            DubLangList = options.DubLangList,
            SubLangList = options.SubLangList,
            OnlyVid = data.Where(a => a.Type == DownloadMediaType.Video).Select(a => new MergerInput{ Language = a.Lang, Path = a.Path ?? string.Empty, Bitrate = a.bitrate }).ToList(),
            SkipSubMux = options.SkipSubMux,
            OnlyAudio = data.Where(a => a.Type == DownloadMediaType.Audio).Select(a => new MergerInput{ Language = a.Lang, Path = a.Path ?? string.Empty, Bitrate = a.bitrate }).ToList(),
            Output = $"{filename}.{(muxToMp3 ? "mp3" : options.Mp4 ? "mp4" : "mkv")}",
            Subtitles = data.Where(a => a.Type == DownloadMediaType.Subtitle).Select(a => new SubtitleInput
                { File = a.Path ?? string.Empty, Language = a.Language, ClosedCaption = a.Cc ?? false, Signs = a.Signs ?? false, RelatedVideoDownloadMedia = a.RelatedVideoDownloadMedia }).ToList(),
            KeepAllVideos = options.KeepAllVideos,
            Fonts = options.MuxFonts ? FontsManager.Instance.MakeFontsList(CfgManager.PathFONTS_DIR, subsList) :[],
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
            DefaultSubSigns = options.DefaultSubSigns,
            DefaultSubForcedDisplay = options.DefaultSubForcedDisplay,
            CcSubsMuxingFlag = options.CcSubsMuxingFlag,
            SignsSubsAsForced = options.SignsSubsAsForced,
            Description = muxDesc ? data.Where(a => a.Type == DownloadMediaType.Description).Select(a => new MergerInput{ Path = a.Path ?? string.Empty }).ToList() :[],
            Cover = options.MuxCover ? data.Where(a => a.Type == DownloadMediaType.Cover).Select(a => new MergerInput{ Path = a.Path ?? string.Empty }).ToList() : [],
        });

        if (!File.Exists(CfgManager.PathFFMPEG)){
            Console.Error.WriteLine("FFmpeg not found");
        }

        if (!File.Exists(CfgManager.PathMKVMERGE)){
            Console.Error.WriteLine("MKVmerge not found");
        }

        bool isMuxed, syncError = false;
        List<string> notSyncedDubs =[];


        if (options is{ SyncTiming: true, DlVideoOnce: true } && merger.options.OnlyVid.Count > 0 && merger.options.OnlyAudio.Count > 0){
            crunchyEpMeta.DownloadProgress = new DownloadProgress(){
                IsDownloading = true,
                Percent = 100,
                Time = 0,
                DownloadSpeed = 0,
                Doing = "Muxing – Syncing Dub Timings"
            };

            QueueManager.Instance.Queue.Refresh();

            var basePath = merger.options.OnlyVid.First().Path;
            var syncVideosList = data.Where(a => a.Type == DownloadMediaType.SyncVideo).ToList();

            if (!string.IsNullOrEmpty(basePath) && syncVideosList.Count > 0){
                foreach (var syncVideo in syncVideosList){
                    if (!string.IsNullOrEmpty(syncVideo.Path)){
                        var delay = await merger.ProcessVideo(basePath, syncVideo.Path);

                        if (delay <= -100){
                            syncError = true;
                            notSyncedDubs.Add(syncVideo.Lang.CrLocale ?? syncVideo.Language.CrLocale);
                            continue;
                        }

                        var audio = merger.options.OnlyAudio.FirstOrDefault(audio => audio.Language.CrLocale == syncVideo.Lang.CrLocale);
                        if (audio != null){
                            audio.Delay = (int)(delay * 1000);
                        }

                        var subtitles = merger.options.Subtitles.Where(a => a.RelatedVideoDownloadMedia == syncVideo).ToList();
                        if (subtitles.Count > 0){
                            foreach (var subMergerInput in subtitles){
                                subMergerInput.Delay = (int)(delay * 1000);
                            }
                        }
                    }
                }
            }

            syncVideosList.ForEach(syncVideo => {
                if (syncVideo.Path != null) Helpers.DeleteFile(syncVideo.Path);
            });

            crunchyEpMeta.DownloadProgress = new DownloadProgress(){
                IsDownloading = true,
                Percent = 100,
                Time = 0,
                DownloadSpeed = 0,
                Doing = "Muxing"
            };

            QueueManager.Instance.Queue.Refresh();
        }

        if (!options.Mp4 && !muxToMp3){
            isMuxed = await merger.Merge("mkvmerge", CfgManager.PathMKVMERGE);
        } else{
            isMuxed = await merger.Merge("ffmpeg", CfgManager.PathFFMPEG);
        }

        return (merger, isMuxed, syncError, string.Join(", ", notSyncedDubs));
    }

    private async Task<DownloadResponse> DownloadMediaList(CrunchyEpMeta data, CrDownloadOptions options){
        if (Profile.Username == "???"){
            MainWindow.Instance.ShowError($"User Account not recognized - are you signed in?");
            return new DownloadResponse{
                Data = new List<DownloadedMedia>(),
                Error = true,
                FileName = "./unknown",
                ErrorText = "User Account not recognized - are you signed in?"
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
            if (!File.Exists(CfgManager.PathFFMPEG)){
                Console.Error.WriteLine("Missing ffmpeg");
                MainWindow.Instance.ShowError($"FFmpeg not found at: {CfgManager.PathFFMPEG}");
                return new DownloadResponse{
                    Data = new List<DownloadedMedia>(),
                    Error = true,
                    FileName = "./unknown",
                    ErrorText = "Missing ffmpeg"
                };
            }

            if (!File.Exists(CfgManager.PathMKVMERGE)){
                Console.Error.WriteLine("Missing Mkvmerge");
                MainWindow.Instance.ShowError($"Mkvmerge not found at: {CfgManager.PathMKVMERGE}");
                return new DownloadResponse{
                    Data = new List<DownloadedMedia>(),
                    Error = true,
                    FileName = "./unknown",
                    ErrorText = "Missing Mkvmerge"
                };
            }
        } else{
            if (!Helpers.IsInstalled("ffmpeg", "-version") && !File.Exists(Path.Combine(AppContext.BaseDirectory, "lib", "ffmpeg"))){
                Console.Error.WriteLine("Ffmpeg is not installed or not in the system PATH.");
                MainWindow.Instance.ShowError("Ffmpeg is not installed on the system or not found in the PATH.");
                return new DownloadResponse{
                    Data = new List<DownloadedMedia>(),
                    Error = true,
                    FileName = "./unknown",
                    ErrorText = "Ffmpeg is not installed"
                };
            }

            if (!Helpers.IsInstalled("mkvmerge", "--version") && !File.Exists(Path.Combine(AppContext.BaseDirectory, "lib", "mkvmerge"))){
                Console.Error.WriteLine("Mkvmerge is not installed or not in the system PATH.");
                MainWindow.Instance.ShowError("Mkvmerge is not installed on the system or not found in the PATH.");
                return new DownloadResponse{
                    Data = new List<DownloadedMedia>(),
                    Error = true,
                    FileName = "./unknown",
                    ErrorText = "Mkvmerge is not installed"
                };
            }
        }

        if (!_widevine.canDecrypt){
            Console.Error.WriteLine("CDM files missing");
            MainWindow.Instance.ShowError("Can't find CDM files in the Widevine folder.\nFor more information, please check the FAQ section in the Wiki on the GitHub page.", true);
            return new DownloadResponse{
                Data = new List<DownloadedMedia>(),
                Error = true,
                FileName = "./unknown",
                ErrorText = "Missing CDM files"
            };
        }

        if (!File.Exists(CfgManager.PathMP4Decrypt) && !File.Exists(CfgManager.PathShakaPackager)){
            Console.Error.WriteLine("mp4decrypt or shaka-packager not found");
            MainWindow.Instance.ShowError($"Either mp4decrypt (expected in lib folder at: {CfgManager.PathMP4Decrypt}) " +
                                          $"or shaka-packager (expected in lib folder at: {CfgManager.PathShakaPackager}) must be available.");
            return new DownloadResponse{
                Data = new List<DownloadedMedia>(),
                Error = true,
                FileName = "./unknown",
                ErrorText = "Requires either mp4decrypt or shaka-packager"
            };
        }

        string mediaName = $"{data.SeasonTitle} - {data.EpisodeNumber} - {data.EpisodeTitle}";
        string fileName = "";
        var variables = new List<Variable>();

        List<DownloadedMedia> files = new List<DownloadedMedia>();

        // if (data.Data != null && data.Data.All(a => a.Playback == null)){
        //     Console.WriteLine("No Video Data found - Are you trying to download a premium episode without havíng a premium account?");
        //     MainWindow.Instance.ShowError("No Video Data found - Are you trying to download a premium episode without havíng a premium account?");
        //     return new DownloadResponse{
        //         Data = files,
        //         Error = true,
        //         FileName = "./unknown",
        //         ErrorText = "Video Data not found"
        //     };
        // }


        bool dlFailed = false;
        bool dlVideoOnce = false;
        string fileDir = CfgManager.PathVIDEOS_DIR;

        if (data.Data is{ Count: > 0 }){
            options.Partsize = options.Partsize > 0 ? options.Partsize : 1;

            var sortedMetaData = data.Data
                .OrderBy(metaData => options.DubLang.IndexOf(metaData.Lang?.CrLocale ?? string.Empty) != -1 ? options.DubLang.IndexOf(metaData.Lang?.CrLocale ?? string.Empty) : int.MaxValue)
                .ToList();

            data.Data = sortedMetaData;

            foreach (CrunchyEpMetaData epMeta in data.Data){
                Console.WriteLine($"Requesting: [{epMeta.MediaId}] {mediaName}");

                string currentMediaId = (epMeta.MediaId.Contains(':') ? epMeta.MediaId.Split(':')[1] : epMeta.MediaId);

                fileDir = options.DownloadToTempFolder ? !string.IsNullOrEmpty(options.DownloadTempDirPath)
                        ? Path.Combine(options.DownloadTempDirPath, Helpers.GetValidFolderName(currentMediaId))
                        : Path.Combine(CfgManager.PathTEMP_DIR, Helpers.GetValidFolderName(currentMediaId)) :
                    !string.IsNullOrEmpty(data.DownloadPath) ? data.DownloadPath :
                    !string.IsNullOrEmpty(options.DownloadDirPath) ? options.DownloadDirPath : CfgManager.PathVIDEOS_DIR;

                if (!Helpers.IsValidPath(fileDir)){
                    fileDir = CfgManager.PathVIDEOS_DIR;
                }


                await CrAuth.RefreshToken(true);

                EpisodeVersion currentVersion = new EpisodeVersion();
                EpisodeVersion primaryVersion = new EpisodeVersion();
                bool isPrimary = epMeta.IsSubbed;

                //Get Media GUID
                string mediaId = epMeta.MediaId;
                string mediaGuid = currentMediaId;
                if (epMeta.Versions != null){
                    if (epMeta.Lang != null){
                        currentVersion = epMeta.Versions.Find(a => a.AudioLocale == epMeta.Lang?.CrLocale) ?? currentVersion;
                    } else if (data.SelectedDubs is{ Count: 1 }){
                        LanguageItem? lang = Array.Find(Languages.languages, a => a.CrLocale == data.SelectedDubs[0]);
                        currentVersion = epMeta.Versions.Find(a => a.AudioLocale == lang?.CrLocale) ?? currentVersion;
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
                        primaryVersion = epMeta.Versions.Find(a => a.Original) ?? currentVersion;
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

                if (options.Chapters && !data.OnlySubs){
                    await ParseChapters(mediaGuid, compiledChapters);

                    if (compiledChapters.Count == 0 && primaryVersion.MediaGuid != null && mediaGuid != primaryVersion.MediaGuid){
                        Console.Error.WriteLine("Chapters empty trying to get original version chapters - might not match with video");
                        await ParseChapters(primaryVersion.MediaGuid, compiledChapters);
                    }
                }

                #endregion

                var fetchPlaybackData = await FetchPlaybackData(options.StreamEndpoint ?? "web/firefox", mediaId, mediaGuid, data.Music);
                (bool IsOk, PlaybackData pbData, string error) fetchPlaybackData2 = default;
                if (!string.IsNullOrEmpty(options.StreamEndpointSecondary) && !(options.StreamEndpoint ?? "web/firefox").Equals(options.StreamEndpointSecondary)){
                    fetchPlaybackData2 = await FetchPlaybackData(options.StreamEndpointSecondary, mediaId, mediaGuid, data.Music);
                }

                if (!fetchPlaybackData.IsOk){
                    var errorJson = fetchPlaybackData.error;
                    if (!string.IsNullOrEmpty(errorJson)){
                        var error = StreamError.FromJson(errorJson);

                        if (error?.IsTooManyActiveStreamsError() == true){
                            MainWindow.Instance.ShowError("Too many active streams that couldn't be stopped");
                            return new DownloadResponse{
                                Data = new List<DownloadedMedia>(),
                                Error = true,
                                FileName = "./unknown",
                                ErrorText = "Too many active streams that couldn't be stopped\nClose open Crunchyroll tabs in your browser"
                            };
                        }

                        if (error?.Error.Contains("Account maturity rating is lower than video rating") == true ||
                            errorJson.Contains("Account maturity rating is lower than video rating")){
                            MainWindow.Instance.ShowError("Account maturity rating is lower than video rating\nChange it in the Crunchyroll account settings");
                            return new DownloadResponse{
                                Data = new List<DownloadedMedia>(),
                                Error = true,
                                FileName = "./unknown",
                                ErrorText = "Account maturity rating is lower than video rating"
                            };
                        }

                        if (!string.IsNullOrEmpty(error?.Error)){
                            MainWindow.Instance.ShowError($"Couldn't get Playback Data\n{error.Error}");
                            return new DownloadResponse{
                                Data = new List<DownloadedMedia>(),
                                Error = true,
                                FileName = "./unknown",
                                ErrorText = "Playback data not found"
                            };
                        }
                    }

                    MainWindow.Instance.ShowError("Couldn't get Playback Data\nTry again later or else check logs and Crunchyroll");
                    return new DownloadResponse{
                        Data = new List<DownloadedMedia>(),
                        Error = true,
                        FileName = "./unknown",
                        ErrorText = "Playback data not found"
                    };
                }

                if (fetchPlaybackData2.IsOk){
                    if (fetchPlaybackData.pbData.Data != null && fetchPlaybackData2.pbData?.Data != null)
                        foreach (var keyValuePair in fetchPlaybackData2.pbData.Data){
                            var pbDataFirstEndpoint = fetchPlaybackData.pbData?.Data;
                            if (pbDataFirstEndpoint != null && pbDataFirstEndpoint.TryGetValue(keyValuePair.Key, out var value)){
                                var urlSecondEndpoint = keyValuePair.Value.Url.First() ?? "";

                                var match = Regex.Match(urlSecondEndpoint, @"(https?:\/\/.*?\/(?:dash\/|\.urlset\/))");
                                var shortendUrl = match.Success ? match.Value : urlSecondEndpoint;

                                if (!value.Url.Any(arrayUrl => arrayUrl != null && arrayUrl.Contains(shortendUrl))){
                                    value.Url.Add(urlSecondEndpoint);
                                }
                            } else{
                                if (pbDataFirstEndpoint != null){
                                    pbDataFirstEndpoint[keyValuePair.Key] = keyValuePair.Value;
                                } else{
                                    if (fetchPlaybackData.pbData != null){
                                        fetchPlaybackData.pbData.Data = new Dictionary<string, StreamDetails>{
                                            [keyValuePair.Key] = keyValuePair.Value
                                        };
                                    }
                                }
                            }
                        }
                }


                var pbData = fetchPlaybackData.pbData;

                List<string> hsLangs = new List<string>();
                var pbStreams = pbData.Data;
                var streams = new List<StreamDetailsPop>();

                variables.Add(new Variable("title", data.EpisodeTitle ?? string.Empty, true));
                variables.Add(new Variable("episode",
                    (double.TryParse(data.EpisodeNumber, NumberStyles.Any, CultureInfo.InvariantCulture, out double episodeNum) ? (object)Math.Round(episodeNum, 1) : data.AbsolutEpisodeNumberE) ?? string.Empty, false));
                variables.Add(new Variable("seriesTitle", data.SeriesTitle ?? string.Empty, true));
                variables.Add(new Variable("seasonTitle", data.SeasonTitle ?? string.Empty, true));
                variables.Add(new Variable("season", !string.IsNullOrEmpty(data.Season) ? Math.Round(double.Parse(data.Season, CultureInfo.InvariantCulture), 1) : 0, false));
                variables.Add(new Variable("dubs", string.Join(", ", data.SelectedDubs ??[]), true));


                if (pbStreams?.Keys != null){
                    var pb = pbStreams.Select(v => {
                        if (v.Value is{ IsHardsubbed: true, HardsubLocale: not null } && v.Value.HardsubLocale != Locale.DefaulT && !hsLangs.Contains(v.Value.HardsubLang.CrLocale)){
                            hsLangs.Add(v.Value.HardsubLang.CrLocale);
                        }

                        return new StreamDetailsPop{
                            Url = v.Value.Url,
                            IsHardsubbed = v.Value.IsHardsubbed,
                            HardsubLocale = v.Value.HardsubLocale,
                            HardsubLang = v.Value.HardsubLang,
                            AudioLang = pbData.Meta?.AudioLocale ?? Languages.DEFAULT_lang,
                            Type = v.Value.Type,
                            Format = "drm_adaptive_dash",
                        };
                    }).ToList();

                    streams.AddRange(pb);


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

                    var audDub = Languages.DEFAULT_lang;
                    if (pbData.Meta != null){
                        audDub = pbData.Meta.AudioLocale;
                    }

                    hsLangs = Languages.SortTags(hsLangs);

                    streams = streams.Select(s => {
                        s.AudioLang = audDub;
                        s.HardsubLang = s.HardsubLang;
                        s.Type = $"{s.Format}/{s.AudioLang.CrLocale}/{s.HardsubLang.CrLocale}";
                        return s;
                    }).ToList();

                    streams.Sort((a, b) => String.CompareOrdinal(a.Type, b.Type));

                    if (options.Hslang != "none"){
                        if (hsLangs.IndexOf(options.Hslang) > -1){
                            Console.WriteLine($"Selecting stream with {Languages.Locale2language(options.Hslang).Language} hardsubs");
                            streams = streams.Where((s) => s.IsHardsubbed && s.HardsubLang.CrLocale == options.Hslang).ToList();
                        } else{
                            Console.Error.WriteLine($"Selected stream with {options.Hslang} hardsubs not available");
                            if (hsLangs.Count > 0){
                                Console.Error.WriteLine("Try hardsubs stream: " + string.Join(", ", hsLangs));
                            }

                            if (dlVideoOnce && options.DlVideoOnce){
                                streams = streams.Where((s) => !s.IsHardsubbed).ToList();
                            } else{
                                if (hsLangs.Count > 0){
                                    var dialog = new ContentDialog(){
                                        Title = "Hardsub Select",
                                        PrimaryButtonText = "Select",
                                        CloseButtonText = "Close"
                                    };

                                    var viewModel = new ContentDialogDropdownSelectViewModel(dialog,
                                        data.SeriesTitle + (!string.IsNullOrEmpty(data.Season)
                                            ? " - S" + data.Season + "E" + (data.EpisodeNumber != string.Empty ? data.EpisodeNumber : data.AbsolutEpisodeNumberE)
                                            : "") + " - " +
                                        data.EpisodeTitle, hsLangs);
                                    dialog.Content = new ContentDialogDropdownSelectView(){
                                        DataContext = viewModel
                                    };

                                    var result = await dialog.ShowAsync();

                                    if (result == ContentDialogResult.Primary){
                                        string selectedValue = viewModel.SelectedDropdownItem.stringValue;

                                        if (hsLangs.IndexOf(selectedValue) > -1){
                                            Console.WriteLine($"Selecting stream with {Languages.Locale2language(selectedValue).Language} hardsubs");
                                            streams = streams.Where((s) => s.IsHardsubbed && s.HardsubLang?.CrLocale == selectedValue).ToList();
                                            data.Hslang = selectedValue;
                                        }
                                    } else{
                                        dlFailed = true;

                                        return new DownloadResponse{
                                            Data = new List<DownloadedMedia>(),
                                            Error = dlFailed,
                                            FileName = "./unknown",
                                            ErrorText = "Hardsub not available"
                                        };
                                    }
                                } else{
                                    dlFailed = true;

                                    return new DownloadResponse{
                                        Data = new List<DownloadedMedia>(),
                                        Error = dlFailed,
                                        FileName = "./unknown",
                                        ErrorText = "No Hardsubs available"
                                    };
                                }
                            }
                        }
                    } else{
                        streams = streams.Where((s) => !s.IsHardsubbed).ToList();

                        if (streams.Count < 1){
                            Console.Error.WriteLine("Raw streams not available!");
                            if (hsLangs.Count > 0){
                                Console.Error.WriteLine("Try hardsubs stream: " + string.Join(", ", hsLangs));
                            }

                            dlFailed = true;
                        }

                        Console.WriteLine("Selecting stream");
                    }

                    StreamDetailsPop? curStream = null;
                    if (!dlFailed){
                        options.Kstream = options.Kstream >= 1 && options.Kstream <= streams.Count
                            ? options.Kstream
                            : 1;

                        for (int i = 0; i < streams.Count; i++){
                            string isSelected = options.Kstream == i + 1 ? "+" : " ";
                            Console.WriteLine($"Full stream: ({isSelected}{i + 1}: {streams[i].Type})");
                        }

                        Console.WriteLine("Downloading video...");
                        curStream = streams[options.Kstream - 1];

                        Console.WriteLine($"Playlists URL: {string.Join(", ", curStream.Url)} ({curStream.Type})");
                    }

                    string tsFile = "";
                    var videoDownloadMedia = new DownloadedMedia(){ Lang = Languages.DEFAULT_lang };

                    if (!dlFailed && curStream != null && options is not{ Novids: true, Noaudio: true }){
                        Dictionary<string, string> streamPlaylistsReqResponseList =[];

                        foreach (var streamUrl in curStream.Url){
                            var streamPlaylistsReq = HttpClientReq.CreateRequestMessage(streamUrl ?? string.Empty, HttpMethod.Get, true, true, null);
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

                            if (streamPlaylistsReqResponse.ResponseContent.Contains("MPD")){
                                streamPlaylistsReqResponseList[streamUrl ?? ""] = streamPlaylistsReqResponse.ResponseContent;
                            }
                        }

                        //Use again when cr has all endpoints with new encoding
                        // var streamPlaylistsReq = HttpClientReq.CreateRequestMessage(curStream.Url ?? string.Empty, HttpMethod.Get, true, true, null);
                        //
                        // var streamPlaylistsReqResponse = await HttpClientReq.Instance.SendHttpRequest(streamPlaylistsReq);
                        //
                        // if (!streamPlaylistsReqResponse.IsOk){
                        //     dlFailed = true;
                        //     return new DownloadResponse{
                        //         Data = new List<DownloadedMedia>(),
                        //         Error = dlFailed,
                        //         FileName = "./unknown",
                        //         ErrorText = "Playlist fetch problem"
                        //     };
                        // }

                        if (dlFailed){
                            Console.WriteLine($"CAN\'T FETCH VIDEO PLAYLISTS!");
                        } else{
                            // if (streamPlaylistsReqResponse.ResponseContent.Contains("MPD")){
                            //     var match = Regex.Match(curStream.Url ?? string.Empty, @"(.*\.urlset\/)");
                            //     var matchedUrl = match.Success ? match.Value : null;
                            //     //Parse MPD Playlists
                            //     var crLocal = "";
                            //     if (pbData.Meta != null){
                            //         crLocal = pbData.Meta.AudioLocale.CrLocale;
                            //     }
                            //
                            //     MPDParsed streamPlaylists = MPDParser.Parse(streamPlaylistsReqResponse.ResponseContent, Languages.FindLang(crLocal), matchedUrl);
                            //
                            //     List<string> streamServers = new List<string>(streamPlaylists.Data.Keys);
                            if (streamPlaylistsReqResponseList.Count > 0){
                                HashSet<string> streamServers =[];
                                Dictionary<string, ServerData> playListData = new Dictionary<string, ServerData>();

                                foreach (var curStreams in streamPlaylistsReqResponseList){
                                    var match = Regex.Match(curStreams.Key ?? string.Empty, @"(https?:\/\/.*?\/(?:dash\/|\.urlset\/))");
                                    var matchedUrl = match.Success ? match.Value : null;
                                    //Parse MPD Playlists
                                    var crLocal = "";
                                    if (pbData.Meta != null){
                                        crLocal = pbData.Meta.AudioLocale.CrLocale;
                                    }

                                    try{
                                        MPDParsed streamPlaylists = MPDParser.Parse(curStreams.Value, Languages.FindLang(crLocal), matchedUrl);
                                        streamServers.UnionWith(streamPlaylists.Data.Keys);
                                        Helpers.MergePlaylistData(playListData, streamPlaylists.Data);
                                    } catch (Exception e){
                                        Console.Error.WriteLine(e);
                                    }
                                }

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

                                // string selectedServer = streamServers[options.StreamServer - 1];
                                // ServerData selectedList = streamPlaylists.Data[selectedServer];

                                string selectedServer = streamServers.ToList()[options.StreamServer - 1];
                                ServerData selectedList = playListData[selectedServer];

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
                                    audioSamplingRate = item.audioSamplingRate,
                                    resolutionText = $"{Math.Round(item.bandwidth / 1000.0)}kB/s"
                                }).ToList();

                                // Video: Remove duplicates by resolution (width, height), keep highest bandwidth, then sort
                                videos = videos
                                    .GroupBy(v => new{ v.quality.width, v.quality.height })
                                    .Select(g => g.OrderByDescending(v => v.bandwidth).First())
                                    .OrderBy(v => v.quality.width)
                                    .ThenBy(v => v.bandwidth)
                                    .ToList();

                                // Audio: Remove duplicates, then sort by bandwidth
                                audios = audios
                                    .GroupBy(a => new{ a.bandwidth, a.language }) // Add more properties if needed
                                    .Select(g => g.First())
                                    .OrderBy(a => a.bandwidth)
                                    .ThenBy(a => a.audioSamplingRate)
                                    .ToList();

                                if (string.IsNullOrEmpty(data.VideoQuality)){
                                    Console.Error.WriteLine("Warning: VideoQuality is null or empty. Defaulting to 'best' quality.");
                                    data.VideoQuality = "best";
                                }

                                int chosenVideoQuality;
                                if (options.DlVideoOnce && dlVideoOnce && options.SyncTiming){
                                    chosenVideoQuality = 1;
                                } else if (data.VideoQuality == "best"){
                                    chosenVideoQuality = videos.Count;
                                } else if (data.VideoQuality == "worst"){
                                    chosenVideoQuality = 1;
                                } else{
                                    var tempIndex = videos.FindIndex(a => a.quality.height + "" == data.VideoQuality?.Replace("p", ""));
                                    if (tempIndex < 0){
                                        chosenVideoQuality = videos.Count;
                                    } else{
                                        tempIndex++;
                                        chosenVideoQuality = tempIndex;
                                    }
                                }

                                if (chosenVideoQuality > videos.Count){
                                    Console.Error.WriteLine($"The requested quality of {chosenVideoQuality} is greater than the maximum {videos.Count}.\n[WARN] Therefore, the maximum will be capped at {videos.Count}.");
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
                                    Console.WriteLine($"\t[{i + 1}] {audios[i].resolutionText} / {audios[i].audioSamplingRate}");
                                }

                                variables.Add(new Variable("height", chosenVideoSegments.quality.height, false));
                                variables.Add(new Variable("width", chosenVideoSegments.quality.width, false));
                                if (string.IsNullOrEmpty(data.Resolution)) data.Resolution = chosenVideoSegments.quality.height + "p";

                                LanguageItem? lang = Languages.languages.FirstOrDefault(a => a.CrLocale == curStream.AudioLang.CrLocale);
                                if (lang == null){
                                    Console.Error.WriteLine($"Unable to find language for code {curStream.AudioLang.CrLocale}");
                                    MainWindow.Instance.ShowError($"Unable to find language for code {curStream.AudioLang.CrLocale}");
                                    return new DownloadResponse{
                                        Data = new List<DownloadedMedia>(),
                                        Error = true,
                                        FileName = "./unknown",
                                        ErrorText = "Language not found"
                                    };
                                }

                                Console.WriteLine($"Selected quality:");
                                Console.WriteLine($"\tVideo: {chosenVideoSegments.resolutionText}");
                                Console.WriteLine($"\tAudio: {chosenAudioSegments.resolutionText} / {chosenAudioSegments.audioSamplingRate}");
                                Console.WriteLine($"\tServer: {selectedServer}");
                                Console.WriteLine("Stream URL:" + chosenVideoSegments.segments[0].uri.Split(new[]{ ",.urlset" }, StringSplitOptions.None)[0]);


                                fileName = Path.Combine(FileNameManager.ParseFileName(options.FileName, variables, options.Numbers, options.FileNameWhitespaceSubstitute, options.Override).ToArray());

                                string onlyFileName = Path.GetFileName(fileName);
                                int maxLength = 220;

                                if (onlyFileName.Length > maxLength){
                                    Console.Error.WriteLine($"Filename too long {onlyFileName}");
                                    if (options.FileName.Split("\\").Last().Contains("${title}") && onlyFileName.Length - (data.EpisodeTitle ?? string.Empty).Length < maxLength){
                                        var titleVariable = variables.Find(e => e.Name == "title");

                                        if (titleVariable != null){
                                            int excessLength = (onlyFileName.Length - maxLength);

                                            if (excessLength > 0 && ((string)titleVariable.ReplaceWith).Length > excessLength){
                                                titleVariable.ReplaceWith = ((string)titleVariable.ReplaceWith).Substring(0, ((string)titleVariable.ReplaceWith).Length - excessLength);
                                                fileName = Path.Combine(FileNameManager.ParseFileName(options.FileName, variables, options.Numbers, options.FileNameWhitespaceSubstitute, options.Override).ToArray());
                                                onlyFileName = Path.GetFileName(fileName);

                                                if (onlyFileName.Length > maxLength){
                                                    fileName = Helpers.LimitFileNameLength(fileName, maxLength);
                                                }
                                            }
                                        }
                                    } else{
                                        fileName = Helpers.LimitFileNameLength(fileName, maxLength);
                                    }

                                    Console.Error.WriteLine($"Filename changed to {Path.GetFileName(fileName)}");
                                }

                                //string outFile = Path.Combine(FileNameManager.ParseFileName(options.FileName + "." + (epMeta.Lang?.CrLocale ?? lang.Value.Name), variables, options.Numbers, options.Override).ToArray());
                                string outFile = fileName + "." + (epMeta.Lang?.CrLocale ?? lang.CrLocale);

                                string tempFile = Path.Combine(FileNameManager
                                    .ParseFileName($"temp-{(!string.IsNullOrEmpty(currentVersion.Guid) ? currentVersion.Guid : currentMediaId)}", variables, options.Numbers, options.FileNameWhitespaceSubstitute,
                                        options.Override)
                                    .ToArray());
                                string tempTsFile = Path.IsPathRooted(tempFile) ? tempFile : Path.Combine(fileDir, tempFile);

                                bool audioDownloaded = false, videoDownloaded = false, syncTimingDownload = false;


                                if (options.DlVideoOnce && dlVideoOnce && !options.SyncTiming){
                                    Console.WriteLine("Already downloaded video, skipping video download...");
                                } else if (options.Novids){
                                    Console.WriteLine("Skipping video download...");
                                } else{
                                    await CrAuth.RefreshToken(true);

                                    Dictionary<string, string> authDataDict = new Dictionary<string, string>
                                        { { "authorization", "Bearer " + Token?.access_token },{ "x-cr-content-id", mediaGuid },{ "x-cr-video-token", pbData.Meta?.Token ?? string.Empty } };

                                    chosenVideoSegments.encryptionKeys = await _widevine.getKeys(chosenVideoSegments.pssh, ApiUrls.WidevineLicenceUrl, authDataDict);

                                    if (!string.IsNullOrEmpty(chosenVideoSegments.pssh) && !chosenVideoSegments.pssh.Equals(chosenAudioSegments.pssh)){
                                        if (chosenAudioSegments.segments.Count > 0 && !options.Noaudio && !dlFailed){
                                            Console.WriteLine("Video and Audio PSSH different requesting Audio encryption keys");
                                            chosenAudioSegments.encryptionKeys = await _widevine.getKeys(chosenAudioSegments.pssh, ApiUrls.WidevineLicenceUrl, authDataDict);
                                        }
                                    }

                                    var videoDownloadResult = await DownloadVideo(chosenVideoSegments, options, outFile, tempTsFile, data, fileDir);

                                    tsFile = videoDownloadResult.tsFile;

                                    if (!videoDownloadResult.Ok){
                                        Console.Error.WriteLine($"Faild to download video - DL Stats: {JsonConvert.SerializeObject(videoDownloadResult.Parts)}");
                                        dlFailed = true;
                                    }

                                    if (options.DlVideoOnce && dlVideoOnce && options.SyncTiming){
                                        syncTimingDownload = true;
                                    }

                                    dlVideoOnce = true;
                                    videoDownloaded = true;
                                }


                                if (chosenAudioSegments.segments.Count > 0 && !options.Noaudio && !dlFailed){
                                    await CrAuth.RefreshToken(true);

                                    if (chosenVideoSegments.encryptionKeys.Count == 0){
                                        Dictionary<string, string> authDataDict = new Dictionary<string, string>
                                            { { "authorization", "Bearer " + Token?.access_token },{ "x-cr-content-id", mediaGuid },{ "x-cr-video-token", pbData.Meta?.Token ?? string.Empty } };

                                        chosenVideoSegments.encryptionKeys = await _widevine.getKeys(chosenVideoSegments.pssh, ApiUrls.WidevineLicenceUrl, authDataDict);

                                        if (!string.IsNullOrEmpty(chosenVideoSegments.pssh) && !chosenVideoSegments.pssh.Equals(chosenAudioSegments.pssh)){
                                            Console.WriteLine("Video and Audio PSSH different requesting Audio encryption keys");
                                            chosenAudioSegments.encryptionKeys = await _widevine.getKeys(chosenAudioSegments.pssh, ApiUrls.WidevineLicenceUrl, authDataDict);
                                        }
                                    }

                                    var audioDownloadResult = await DownloadAudio(chosenAudioSegments, options, outFile, tempTsFile, data, fileDir);

                                    tsFile = audioDownloadResult.tsFile;

                                    if (!audioDownloadResult.Ok){
                                        Console.Error.WriteLine($"Faild to download audio - DL Stats: {JsonConvert.SerializeObject(audioDownloadResult.Parts)}");
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
                                        ErrorText = "Audio or Video download failed",
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
                                    QueueManager.Instance.Queue.Refresh();

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

                                    await CrAuth.RefreshToken(true);

                                    Dictionary<string, string> authDataDict = new Dictionary<string, string>
                                        { { "authorization", "Bearer " + Token?.access_token },{ "x-cr-content-id", mediaGuid },{ "x-cr-video-token", pbData.Meta?.Token ?? string.Empty } };

                                    var encryptionKeys = chosenVideoSegments.encryptionKeys;

                                    if (encryptionKeys.Count == 0){
                                        encryptionKeys = await _widevine.getKeys(chosenVideoSegments.pssh, ApiUrls.WidevineLicenceUrl, authDataDict);

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
                                    }


                                    List<ContentKey> encryptionKeysAudio = chosenAudioSegments.encryptionKeys;
                                    if (!string.IsNullOrEmpty(chosenVideoSegments.pssh) && !chosenVideoSegments.pssh.Equals(chosenAudioSegments.pssh)){
                                        Console.WriteLine("Video and Audio PSSH different requesting Audio encryption keys");
                                        encryptionKeysAudio = await _widevine.getKeys(chosenAudioSegments.pssh, ApiUrls.WidevineLicenceUrl, authDataDict);
                                        if (encryptionKeysAudio.Count == 0){
                                            Console.Error.WriteLine("Failed to get audio encryption keys");
                                            dlFailed = true;
                                            return new DownloadResponse{
                                                Data = files,
                                                Error = dlFailed,
                                                FileName = fileName.Length > 0 ? (Path.IsPathRooted(fileName) ? fileName : Path.Combine(fileDir, fileName)) : "./unknown",
                                                ErrorText = "Couldn't get DRM audio encryption keys"
                                            };
                                        }
                                    }

                                    if (Path.Exists(CfgManager.PathMP4Decrypt) || Path.Exists(CfgManager.PathShakaPackager)){
                                        var tempTsFileName = Path.GetFileName(tempTsFile);
                                        var tempTsFileWorkDir = Path.GetDirectoryName(tempTsFile) ?? CfgManager.PathVIDEOS_DIR;

                                        // Use audio keys if available, otherwise fallback to video keys
                                        var audioKeysToUse = encryptionKeysAudio.Count > 0 ? encryptionKeysAudio : encryptionKeys;

                                        // === mp4decrypt command ===
                                        var videoKey = encryptionKeys[0];
                                        var videoKeyParam = BuildMp4DecryptKeyParam(videoKey.KeyID, videoKey.Bytes);
                                        var commandVideo = $"--show-progress {videoKeyParam} \"{tempTsFileName}.video.enc.m4s\" \"{tempTsFileName}.video.m4s\"";

                                        var audioKey = audioKeysToUse[0];
                                        var audioKeyParam = BuildMp4DecryptKeyParam(audioKey.KeyID, audioKey.Bytes);
                                        var commandAudio = $"--show-progress {audioKeyParam} \"{tempTsFileName}.audio.enc.m4s\" \"{tempTsFileName}.audio.m4s\"";

                                        bool shaka = Path.Exists(CfgManager.PathShakaPackager);
                                        if (shaka){
                                            // === shaka-packager command ===
                                            var shakaVideoKeys = BuildShakaKeysParam(encryptionKeys);
                                            commandVideo = $"input=\"{tempTsFileName}.video.enc.m4s\",stream=video,output=\"{tempTsFileName}.video.m4s\" {shakaVideoKeys}";

                                            var shakaAudioKeys = BuildShakaKeysParam(audioKeysToUse);
                                            commandAudio = $"input=\"{tempTsFileName}.audio.enc.m4s\",stream=audio,output=\"{tempTsFileName}.audio.m4s\" {shakaAudioKeys}";
                                        }

                                        if (videoDownloaded){
                                            Console.WriteLine("Started decrypting video");
                                            data.DownloadProgress = new DownloadProgress(){
                                                IsDownloading = true,
                                                Percent = 100,
                                                Time = 0,
                                                DownloadSpeed = 0,
                                                Doing = "Decrypting video"
                                            };
                                            QueueManager.Instance.Queue.Refresh();
                                            var decryptVideo = await Helpers.ExecuteCommandAsyncWorkDir(shaka ? "shaka-packager" : "mp4decrypt", shaka ? CfgManager.PathShakaPackager : CfgManager.PathMP4Decrypt,
                                                commandVideo, tempTsFileWorkDir);

                                            if (!decryptVideo.IsOk){
                                                Console.Error.WriteLine($"Decryption failed with exit code {decryptVideo.ErrorCode}");
                                                MainWindow.Instance.ShowError($"Decryption failed with exit code {decryptVideo.ErrorCode}");
                                                try{
                                                    File.Move($"{tempTsFile}.video.enc.m4s", $"{tsFile}.video.enc.m4s");
                                                } catch (IOException ex){
                                                    Console.WriteLine($"An error occurred: {ex.Message}");
                                                }

                                                dlFailed = true;
                                                return new DownloadResponse{
                                                    Data = files,
                                                    Error = dlFailed,
                                                    FileName = fileName.Length > 0 ? (Path.IsPathRooted(fileName) ? fileName : Path.Combine(fileDir, fileName)) : "./unknown",
                                                    ErrorText = "Decryption failed"
                                                };
                                            }

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

                                            videoDownloadMedia = new DownloadedMedia{
                                                Type = syncTimingDownload ? DownloadMediaType.SyncVideo : DownloadMediaType.Video,
                                                Path = $"{tsFile}.video.m4s",
                                                Lang = lang,
                                                Language = lang,
                                                IsPrimary = isPrimary,
                                                bitrate = chosenVideoSegments.bandwidth / 1024
                                            };
                                            files.Add(videoDownloadMedia);
                                            data.downloadedFiles.Add($"{tsFile}.video.m4s");
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
                                            QueueManager.Instance.Queue.Refresh();
                                            var decryptAudio = await Helpers.ExecuteCommandAsyncWorkDir(shaka ? "shaka-packager" : "mp4decrypt", shaka ? CfgManager.PathShakaPackager : CfgManager.PathMP4Decrypt,
                                                commandAudio, tempTsFileWorkDir);

                                            if (!decryptAudio.IsOk){
                                                Console.Error.WriteLine($"Decryption failed with exit code {decryptAudio.ErrorCode}");
                                                try{
                                                    File.Move($"{tempTsFile}.audio.enc.m4s", $"{tsFile}.audio.enc.m4s");
                                                } catch (IOException ex){
                                                    Console.WriteLine($"An error occurred: {ex.Message}");
                                                }

                                                dlFailed = true;
                                                return new DownloadResponse{
                                                    Data = files,
                                                    Error = dlFailed,
                                                    FileName = fileName.Length > 0 ? (Path.IsPathRooted(fileName) ? fileName : Path.Combine(fileDir, fileName)) : "./unknown",
                                                    ErrorText = "Decryption failed"
                                                };
                                            }

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
                                                Lang = lang,
                                                IsPrimary = isPrimary,
                                                bitrate = chosenAudioSegments.bandwidth / 1000
                                            });
                                            data.downloadedFiles.Add($"{tsFile}.audio.m4s");
                                        } else{
                                            Console.WriteLine("No Audio downloaded");
                                        }
                                    } else{
                                        Console.Error.WriteLine("mp4decrypt not found, files need decryption. Decryption Keys: ");
                                        MainWindow.Instance.ShowError($"mp4decrypt not found, files need decryption");
                                    }
                                } else{
                                    if (videoDownloaded){
                                        videoDownloadMedia = new DownloadedMedia{
                                            Type = syncTimingDownload ? DownloadMediaType.SyncVideo : DownloadMediaType.Video,
                                            Path = $"{tsFile}.video.m4s",
                                            Lang = lang,
                                            Language = lang,
                                            IsPrimary = isPrimary,
                                            bitrate = chosenVideoSegments.bandwidth / 1024
                                        };
                                        files.Add(videoDownloadMedia);
                                        data.downloadedFiles.Add($"{tsFile}.video.m4s");
                                    }

                                    if (audioDownloaded){
                                        files.Add(new DownloadedMedia{
                                            Type = DownloadMediaType.Audio,
                                            Path = $"{tsFile}.audio.m4s",
                                            Lang = lang,
                                            IsPrimary = isPrimary,
                                            bitrate = chosenAudioSegments.bandwidth / 1000
                                        });
                                        data.downloadedFiles.Add($"{tsFile}.audio.m4s");
                                    }
                                }
                            } else if (options.Novids){
                                fileName = Path.Combine(FileNameManager.ParseFileName(options.FileName, variables, options.Numbers, options.FileNameWhitespaceSubstitute, options.Override).ToArray());
                                Console.WriteLine("Downloading skipped!");
                            }
                        }
                    } else if (options is{ Novids: true, Noaudio: true }){
                        variables.Add(new Variable("height", 360, false));
                        variables.Add(new Variable("width", 640, false));

                        fileName = Path.Combine(FileNameManager.ParseFileName(options.FileName, variables, options.Numbers, options.FileNameWhitespaceSubstitute, options.Override).ToArray());
                    }

                    if (compiledChapters.Count > 0 && options is not{ Novids: true, Noaudio: true }){
                        try{
                            // Parsing and constructing the file names
                            fileName = Path.Combine(FileNameManager.ParseFileName(options.FileName, variables, options.Numbers, options.FileNameWhitespaceSubstitute, options.Override).ToArray());
                            var outFile = Path.Combine(FileNameManager.ParseFileName(options.FileName + "." + (epMeta.Lang?.CrLocale), variables, options.Numbers, options.FileNameWhitespaceSubstitute, options.Override)
                                .ToArray());
                            if (Path.IsPathRooted(outFile)){
                                tsFile = outFile;
                            } else{
                                tsFile = Path.Combine(fileDir, outFile);
                            }

                            // Check if the path is absolute
                            var isAbsolute = Path.IsPathRooted(outFile);

                            // Get all directory parts of the path except the last segment (assuming it's a file)
                            var directories = Path.GetDirectoryName(outFile)?.Split(Path.DirectorySeparatorChar) ??[];

                            // Initialize the cumulative path based on whether the original path is absolute or not
                            var cumulativePath = isAbsolute ? "" : fileDir;
                            for (var i = 0; i < directories.Length; i++){
                                // Build the path incrementally
                                cumulativePath = Path.Combine(cumulativePath, directories[i]);

                                // Check if the directory exists and create it if it does not
                                if (!Directory.Exists(cumulativePath)){
                                    Directory.CreateDirectory(cumulativePath);
                                    Console.WriteLine($"Created directory: {cumulativePath}");
                                }
                            }

                            // Finding language by code
                            var lang = Languages.languages.FirstOrDefault(l => l == curStream?.AudioLang) ?? Languages.DEFAULT_lang;
                            if (lang.Code == "und"){
                                Console.Error.WriteLine($"Unable to find language for code {curStream?.AudioLang}");
                            }

                            File.WriteAllText($"{tsFile}.txt", string.Join("\r\n", compiledChapters));

                            files.Add(new DownloadedMedia{ Path = $"{tsFile}.txt", Lang = lang, Type = DownloadMediaType.Chapters });
                            data.downloadedFiles.Add($"{tsFile}.txt");
                        } catch{
                            Console.Error.WriteLine("Failed to write chapter file");
                        }
                    }

                    if (data.DownloadSubs.IndexOf("all") > -1){
                        data.DownloadSubs = new List<string>{ "all" };
                    }

                    if (options.Hslang != "none"){
                        Console.WriteLine("Subtitles downloading disabled for hardsubed streams.");
                        options.SkipSubs = true;
                    }

                    if (!options.SkipSubs && data.DownloadSubs.IndexOf("none") == -1){
                        await DownloadSubtitles(options, pbData, audDub, fileName, files, fileDir, data, videoDownloadMedia);
                    } else{
                        Console.WriteLine("Subtitles downloading skipped!");
                    }
                }

                // await Task.Delay(options.Waittime);
            }
        }

        if (options.IncludeVideoDescription && !data.OnlySubs){
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

                files.Add(new DownloadedMedia{
                    Type = DownloadMediaType.Description,
                    Path = fullPath,
                    Lang = Languages.DEFAULT_lang
                });
                data.downloadedFiles.Add(fullPath);
            } else{
                if (files.All(e => e.Type != DownloadMediaType.Description)){
                    files.Add(new DownloadedMedia{
                        Type = DownloadMediaType.Description,
                        Path = fullPath,
                        Lang = Languages.DEFAULT_lang
                    });
                    data.downloadedFiles.Add(fullPath);
                }
            }

            Console.WriteLine($"{fileName}.xml has been created with the description.");
        }
        
        if (options.MuxCover){
            if (!string.IsNullOrEmpty(data.ImageBig) && !File.Exists(fileDir + "cover.png")){
                var bitmap = await Helpers.LoadImage(data.ImageBig);
                if (bitmap != null){
                    string coverPath = Path.Combine(fileDir, "cover.png");
                    Helpers.EnsureDirectoriesExist(coverPath);
                    await using (var fs = File.OpenWrite(coverPath)){
                        bitmap.Save(fs); // always saves PNG
                    }
                    bitmap.Dispose();
                            
                    files.Add(new DownloadedMedia{
                        Type = DownloadMediaType.Cover,
                        Lang = Languages.DEFAULT_lang,
                        Path = coverPath
                    });
                            
                }
            }
        }

        var tempFolderPath = "";
        if (options.DownloadToTempFolder){
            tempFolderPath = fileDir;
            fileDir = !string.IsNullOrEmpty(data.DownloadPath) ? data.DownloadPath :
                !string.IsNullOrEmpty(options.DownloadDirPath) ? options.DownloadDirPath : CfgManager.PathVIDEOS_DIR;
        }

        if (string.IsNullOrEmpty(data.DownloadPath)){
            data.DownloadPath = fileDir;
        }

        return new DownloadResponse{
            Data = files,
            Error = dlFailed,
            FileName = fileName.Length > 0 ? fileName : "unknown - " + Guid.NewGuid(),
            ErrorText = "",
            VideoTitle = FileNameManager.ParseFileName(options.VideoTitle ?? "", variables, options.Numbers, options.FileNameWhitespaceSubstitute, options.Override).Last(),
            FolderPath = fileDir,
            TempFolderPath = tempFolderPath
        };
    }

    private static async Task DownloadSubtitles(CrDownloadOptions options, PlaybackData pbData, LanguageItem audDub, string fileName, List<DownloadedMedia> files, string fileDir, CrunchyEpMeta data,
        DownloadedMedia videoDownloadMedia){
        if (pbData.Meta != null && (pbData.Meta.Subtitles is{ Count: > 0 } || pbData.Meta.Captions is{ Count: > 0 })){
            if (videoDownloadMedia.Lang == Languages.DEFAULT_lang){
                videoDownloadMedia.Lang = pbData.Meta.AudioLocale;
            }

            List<SubtitleInfo> subsData = pbData.Meta.Subtitles?.Values.ToList() ??[];
            List<Caption> capsData = pbData.Meta.Captions?.Values.ToList() ??[];
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
                var subLang = Languages.FixAndFindCrLc((s.Language ?? Locale.DefaulT).GetEnumMemberValue());
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
                var isSigns = langItem.CrLocale == audDub.CrLocale && !subsItem.isCC;
                var isCc = subsItem.isCC;
                var isDuplicate = false;


                if ((!options.IncludeSignsSubs && isSigns) || (!options.IncludeCcSubs && isCc)){
                    continue;
                }

                var matchingSubs = files.Where(a => a.Type == DownloadMediaType.Subtitle &&
                                                    (a.Language.CrLocale == langItem.CrLocale || a.Language.Locale == langItem.Locale) &&
                                                    a.Cc == isCc &&
                                                    a.Signs == isSigns).ToList();

                if (matchingSubs.Count > 0){
                    isDuplicate = true;
                    if (!options.SubsDownloadDuplicate || matchingSubs.Any(a => a.RelatedVideoDownloadMedia?.Lang == videoDownloadMedia.Lang)){
                        continue;
                    }
                }

                sxData.File = Languages.SubsFile(fileName, index + "", langItem, isDuplicate ? videoDownloadMedia.Lang.CrLocale : "", isCc, options.CcTag, isSigns, subsItem.format,
                    !(data.DownloadSubs.Count == 1 && !data.DownloadSubs.Contains("all")));
                sxData.Path = Path.Combine(fileDir, sxData.File);

                Helpers.EnsureDirectoriesExist(sxData.Path);

                if (data.DownloadSubs.Contains("all") || data.DownloadSubs.Contains(langItem.CrLocale)){
                    if (string.IsNullOrEmpty(subsItem.url)){
                        continue;
                    }

                    var subsAssReq = HttpClientReq.CreateRequestMessage(subsItem.url, HttpMethod.Get, false, false, null);

                    var subsAssReqResponse = await HttpClientReq.Instance.SendHttpRequest(subsAssReq);

                    if (subsAssReqResponse.IsOk){
                        if (subsItem.format == "ass"){
                            var sBodySplit = subsAssReqResponse.ResponseContent.Split(new[]{ "\r\n" }, StringSplitOptions.None).ToList();

                            if (sBodySplit.Count > 2){
                                if (options.SubsAddScaledBorder == ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes){
                                    sBodySplit.Insert(2, "ScaledBorderAndShadow: yes");
                                } else if (options.SubsAddScaledBorder == ScaledBorderAndShadowSelection.ScaledBorderAndShadowNo){
                                    sBodySplit.Insert(2, "ScaledBorderAndShadow: no");
                                }
                            }

                            subsAssReqResponse.ResponseContent = string.Join("\r\n", sBodySplit);

                            if (sBodySplit.Count > 1){
                                sxData.Title = sBodySplit[1].Replace("Title: ", "");
                                sxData.Title = $"{langItem.Language} / {sxData.Title}";
                                var keysList = FontsManager.ExtractFontsFromAss(subsAssReqResponse.ResponseContent);
                                sxData.Fonts = FontsManager.Instance.GetDictFromKeyList(keysList);
                            }
                        } else if (subsItem.format == "vtt"){
                            var assBuilder = new StringBuilder();

                            assBuilder.AppendLine("[Script Info]");
                            assBuilder.AppendLine("Title: CC Subtitle");
                            assBuilder.AppendLine("ScriptType: v4.00+");
                            assBuilder.AppendLine("WrapStyle: 0");
                            assBuilder.AppendLine("PlayResX: 640");
                            assBuilder.AppendLine("PlayResY: 360");
                            assBuilder.AppendLine("Timer: 0.0");
                            if (options.SubsAddScaledBorder == ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes){
                                assBuilder.AppendLine("ScaledBorderAndShadow: yes");
                            } else if (options.SubsAddScaledBorder == ScaledBorderAndShadowSelection.ScaledBorderAndShadowNo){
                                assBuilder.AppendLine("ScaledBorderAndShadow: no");
                            }

                            assBuilder.AppendLine();
                            assBuilder.AppendLine("[V4+ Styles]");
                            assBuilder.AppendLine("Format: Name,Fontname,Fontsize,PrimaryColour,SecondaryColour,OutlineColour,BackColour,Bold,Italic,"
                                                  + "Underline,Strikeout,ScaleX,ScaleY,Spacing,Angle,BorderStyle,Outline,Shadow,Alignment,MarginL,MarginR,MarginV,Encoding");
                            assBuilder.AppendLine($"Style: Default,{options.CcSubsFont ?? "Trebuchet MS"},24,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,2,0010,0010,0018,1");
                            assBuilder.AppendLine();
                            assBuilder.AppendLine("[Events]");
                            assBuilder.AppendLine("Format: Layer,Start,End,Style,Name,MarginL,MarginR,MarginV,Effect,Text");

                            // Parse the VTT content
                            string normalizedContent = subsAssReqResponse.ResponseContent.Replace("\r\n", "\n").Replace("\r", "\n");
                            var blocks = normalizedContent.Split(new[]{ "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                            Regex timePattern = new Regex(@"(?<start>\d{2}:\d{2}:\d{2}\.\d{3})\s-->\s(?<end>\d{2}:\d{2}:\d{2}\.\d{3})");

                            foreach (var block in blocks){
                                // Split each block into lines
                                var lines = block.Split(new[]{ '\n' }, StringSplitOptions.RemoveEmptyEntries);

                                if (lines.Length < 3) continue; // Skip blocks that don't have enough lines

                                // Match the first line to get the time codes
                                Match match = timePattern.Match(lines[1]);

                                if (match.Success){
                                    string startTime = Helpers.ConvertTimeFormat(match.Groups["start"].Value);
                                    string endTime = Helpers.ConvertTimeFormat(match.Groups["end"].Value);

                                    // Join the remaining lines for dialogue, using \N for line breaks
                                    string dialogue = string.Join("\\N", lines.Skip(2));

                                    dialogue = Helpers.ConvertVTTStylesToASS(dialogue);

                                    // Append dialogue to ASS
                                    assBuilder.AppendLine($"Dialogue: 0,{startTime},{endTime},Default,,0000,0000,0000,,{dialogue}");
                                }
                            }

                            subsAssReqResponse.ResponseContent = assBuilder.ToString();

                            sxData.Title = $"{langItem.Name} / CC Subtitle";
                            var keysList = FontsManager.ExtractFontsFromAss(subsAssReqResponse.ResponseContent);
                            sxData.Fonts = FontsManager.Instance.GetDictFromKeyList(keysList);
                            sxData.Path = sxData.Path.Replace("vtt", "ass");
                        }

                        File.WriteAllText(sxData.Path, subsAssReqResponse.ResponseContent);
                        Console.WriteLine($"Subtitle downloaded: {sxData.File}");
                        files.Add(new DownloadedMedia{
                            Type = DownloadMediaType.Subtitle,
                            Cc = isCc,
                            Signs = isSigns,
                            Path = sxData.Path,
                            File = sxData.File,
                            Title = sxData.Title,
                            Fonts = sxData.Fonts,
                            Language = sxData.Language,
                            Lang = sxData.Language,
                            RelatedVideoDownloadMedia = videoDownloadMedia
                        });
                        data.downloadedFiles.Add(sxData.Path);
                    } else{
                        Console.WriteLine($"Failed to download subtitle: ${sxData.File}");
                    }
                }
            }
        } else{
            Console.WriteLine("Can\'t find urls for subtitles!");
        }
    }

    private async Task<(bool Ok, PartsData Parts, string tsFile)> DownloadVideo(VideoItem chosenVideoSegments, CrDownloadOptions options, string outFile, string tempTsFile, CrunchyEpMeta data,
        string fileDir){
        // Prepare for video download
        int totalParts = chosenVideoSegments.segments.Count;
        int mathParts = (int)Math.Ceiling((double)totalParts / options.Partsize);
        string mathMsg = $"({mathParts}*{options.Partsize})";
        string tsFile;
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

        data.downloadedFiles.Add(chosenVideoSegments.pssh != null ? $"{tempTsFile}.video.enc.m4s" : $"{tsFile}.video.m4s");
        data.downloadedFiles.Add(chosenVideoSegments.pssh != null ? $"{tempTsFile}.video.enc.m4s.resume" : $"{tsFile}.video.m4s.resume");

        var videoDownloader = new HlsDownloader(new HlsOptions{
            Output = chosenVideoSegments.pssh != null ? $"{tempTsFile}.video.enc.m4s" : $"{tsFile}.video.m4s",
            Timeout = options.Timeout,
            M3U8Json = videoJson,
            // BaseUrl = chunkPlaylist.BaseUrl, 
            Threads = options.Partsize,
            FsRetryTime = options.RetryDelay * 1000,
            Retries = options.RetryAttempts,
            Override = options.Force,
        }, data, true, false, options.DownloadMethodeNew);

        var defParts = new PartsData{
            First = 0,
            Total = 0,
            Completed = 0,
        };

        var videoDownloadResult = (Ok: false, Parts: defParts);

        try{
            videoDownloadResult = await videoDownloader.Download();
        } catch (Exception e){
            Console.WriteLine(e);
        }


        return (videoDownloadResult.Ok, videoDownloadResult.Parts, tsFile);
    }

    private async Task<(bool Ok, PartsData Parts, string tsFile)> DownloadAudio(AudioItem chosenAudioSegments, CrDownloadOptions options, string outFile, string tempTsFile, CrunchyEpMeta data,
        string fileDir){
        // Prepare for audio download
        string tsFile;
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

        data.downloadedFiles.Add(chosenAudioSegments.pssh != null ? $"{tempTsFile}.audio.enc.m4s" : $"{tsFile}.audio.m4s");
        data.downloadedFiles.Add(chosenAudioSegments.pssh != null ? $"{tempTsFile}.audio.enc.m4s.resume" : $"{tsFile}.audio.m4s.resume");

        var audioDownloader = new HlsDownloader(new HlsOptions{
            Output = chosenAudioSegments.pssh != null ? $"{tempTsFile}.audio.enc.m4s" : $"{tsFile}.audio.m4s",
            Timeout = options.Timeout,
            M3U8Json = audioJson,
            // BaseUrl = chunkPlaylist.BaseUrl, 
            Threads = options.Partsize,
            FsRetryTime = options.RetryDelay * 1000,
            Retries = options.RetryAttempts,
            Override = options.Force,
        }, data, false, true, options.DownloadMethodeNew);

        var defParts = new PartsData{
            First = 0,
            Total = 0,
            Completed = 0,
        };

        var audioDownloadResult = (Ok: false, Parts: defParts);

        try{
            audioDownloadResult = await audioDownloader.Download();
        } catch (Exception e){
            Console.WriteLine(e);
        }

        return (audioDownloadResult.Ok, audioDownloadResult.Parts, tsFile);
    }

    #region Fetch Playback Data

    private async Task<(bool IsOk, PlaybackData pbData, string error)> FetchPlaybackData(string streamEndpoint, string mediaId, string mediaGuidId, bool music){
        var temppbData = new PlaybackData{
            Total = 0,
            Data = new Dictionary<string, StreamDetails>()
        };

        var playbackEndpoint = $"{ApiUrls.Playback}/{(music ? "music/" : "")}{mediaGuidId}/{streamEndpoint}/play";
        var playbackRequestResponse = await SendPlaybackRequestAsync(playbackEndpoint);

        if (!playbackRequestResponse.IsOk){
            playbackRequestResponse = await HandleStreamErrorsAsync(playbackRequestResponse, playbackEndpoint);
        }

        if (playbackRequestResponse.IsOk){
            temppbData = await ProcessPlaybackResponseAsync(playbackRequestResponse.ResponseContent, mediaId, mediaGuidId);
        } else{
            Console.WriteLine("Request Stream URLs FAILED! Attempting fallback");
            playbackEndpoint = $"{ApiUrls.Playback}/{(music ? "music/" : "")}{mediaGuidId}/web/firefox/play";
            playbackRequestResponse = await SendPlaybackRequestAsync(playbackEndpoint);

            if (!playbackRequestResponse.IsOk){
                playbackRequestResponse = await HandleStreamErrorsAsync(playbackRequestResponse, playbackEndpoint);
            }

            if (playbackRequestResponse.IsOk){
                temppbData = await ProcessPlaybackResponseAsync(playbackRequestResponse.ResponseContent, mediaId, mediaGuidId);
            } else{
                Console.Error.WriteLine("Fallback Request Stream URLs FAILED!");
            }
        }

        return (playbackRequestResponse.IsOk, pbData: temppbData, error: playbackRequestResponse.IsOk ? "" : playbackRequestResponse.ResponseContent);
    }

    private async Task<(bool IsOk, string ResponseContent, string error)> SendPlaybackRequestAsync(string endpoint){
        var request = HttpClientReq.CreateRequestMessage(endpoint, HttpMethod.Get, true, false, null);
        return await HttpClientReq.Instance.SendHttpRequest(request);
    }

    private async Task<(bool IsOk, string ResponseContent, string error)> HandleStreamErrorsAsync((bool IsOk, string ResponseContent, string error) response, string endpoint){
        if (response.IsOk || string.IsNullOrEmpty(response.ResponseContent)) return response;

        var error = StreamError.FromJson(response.ResponseContent);
        if (error?.IsTooManyActiveStreamsError() == true){
            foreach (var errorActiveStream in error.ActiveStreams){
                await HttpClientReq.DeAuthVideo(errorActiveStream.ContentId, errorActiveStream.Token);
            }

            return await SendPlaybackRequestAsync(endpoint);
        }

        return response;
    }

    private async Task<PlaybackData> ProcessPlaybackResponseAsync(string responseContent, string mediaId, string mediaGuidId){
        var temppbData = new PlaybackData{
            Total = 0,
            Data = new Dictionary<string, StreamDetails>()
        };

        var playStream = Helpers.Deserialize<CrunchyStreamData>(responseContent, SettingsJsonSerializerSettings);
        if (playStream == null) return temppbData;

        if (!string.IsNullOrEmpty(playStream.Token)){
            await HttpClientReq.DeAuthVideo(mediaGuidId, playStream.Token);
        }

        var derivedPlayCrunchyStreams = new CrunchyStreams();
        if (playStream.HardSubs != null){
            //hlang "none" is no hardsube same url as the default url
            foreach (var hardsub in playStream.HardSubs){
                var stream = hardsub.Value;
                derivedPlayCrunchyStreams[hardsub.Key] = new StreamDetails{
                    Url =[stream.Url],
                    IsHardsubbed = true,
                    HardsubLocale = stream.Hlang,
                    HardsubLang = Languages.FixAndFindCrLc((stream.Hlang ?? Locale.DefaulT).GetEnumMemberValue())
                };
            }
        }

        derivedPlayCrunchyStreams[""] = new StreamDetails{
            Url =[playStream.Url],
            IsHardsubbed = false,
            HardsubLocale = Locale.DefaulT,
            HardsubLang = Languages.DEFAULT_lang
        };

        temppbData.Data = derivedPlayCrunchyStreams;
        temppbData.Total = 1;

        temppbData.Meta = new PlaybackMeta{
            AudioLocale = Languages.FindLang(playStream.AudioLocale != null ? playStream.AudioLocale.GetEnumMemberValue() : ""),
            Versions = playStream.Versions,
            Bifs = new List<string>{ playStream.Bifs ?? "" },
            MediaId = mediaId,
            Captions = playStream.Captions,
            Subtitles = new Subtitles(),
            Token = playStream.Token,
        };

        if (playStream.Subtitles != null){
            foreach (var subtitle in playStream.Subtitles){
                temppbData.Meta.Subtitles.Add(subtitle.Key, new SubtitleInfo{
                    Format = subtitle.Value.Format,
                    Locale = subtitle.Value.Locale,
                    Url = subtitle.Value.Url
                });
            }
        }

        return temppbData;
    }

    #endregion


    private async Task ParseChapters(string currentMediaId, List<string> compiledChapters){
        var showRequest = HttpClientReq.CreateRequestMessage($"https://static.crunchyroll.com/skip-events/production/{currentMediaId}.json", HttpMethod.Get, true, true, null);

        var showRequestResponse = await HttpClientReq.Instance.SendHttpRequest(showRequest, true);

        if (showRequestResponse.IsOk){
            CrunchyChapters chapterData = new CrunchyChapters();
            chapterData.Chapters = new List<CrunchyChapter>();

            try{
                JObject jObject = JObject.Parse(showRequestResponse.ResponseContent);

                if (jObject.TryGetValue("lastUpdate", out JToken? lastUpdateToken)){
                    chapterData.lastUpdate = lastUpdateToken.ToObject<DateTime>();
                }

                if (jObject.TryGetValue("mediaId", out JToken? mediaIdToken)){
                    chapterData.mediaId = mediaIdToken.ToObject<string>();
                }

                chapterData.Chapters = new List<CrunchyChapter>();

                foreach (var property in jObject.Properties()){
                    if (property.Value.Type == JTokenType.Object && property.Name != "lastUpdate" && property.Name != "mediaId"){
                        try{
                            CrunchyChapter chapter = property.Value.ToObject<CrunchyChapter>() ?? new CrunchyChapter();
                            chapterData.Chapters.Add(chapter);
                        } catch (Exception ex){
                            Console.Error.WriteLine($"Error parsing chapter: {ex.Message}");
                        }
                    }
                }
            } catch (Exception ex){
                Console.Error.WriteLine($"Error parsing JSON response: {ex.Message}");
                return;
            }

            if (chapterData.Chapters.Count > 0){
                chapterData.Chapters.Sort((a, b) => {
                    if (a.start.HasValue && b.start.HasValue){
                        return a.start.Value.CompareTo(b.start.Value);
                    }

                    return 0; // Both values are null, they are considered equal
                });

                if (!((chapterData.Chapters.Any(c => c.type == "intro")) || chapterData.Chapters.Any(c => c.type == "recap"))){
                    int chapterNumber = (compiledChapters.Count / 2) + 1;
                    compiledChapters.Add($"CHAPTER{chapterNumber}=00:00:00.00");
                    compiledChapters.Add($"CHAPTER{chapterNumber}NAME=Episode");
                }

                foreach (CrunchyChapter chapter in chapterData.Chapters){
                    if (chapter.start == null || chapter.end == null) continue;

                    TimeSpan startTime = TimeSpan.FromSeconds(chapter.start.Value);
                    TimeSpan endTime = TimeSpan.FromSeconds(chapter.end.Value);

                    string startFormatted = startTime.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
                    string endFormatted = endTime.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

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

            showRequestResponse = await HttpClientReq.Instance.SendHttpRequest(showRequest, true);

            if (showRequestResponse.IsOk){
                CrunchyOldChapter chapterData = Helpers.Deserialize<CrunchyOldChapter>(showRequestResponse.ResponseContent, SettingsJsonSerializerSettings) ?? new CrunchyOldChapter();

                TimeSpan startTime = TimeSpan.FromSeconds(chapterData.startTime);
                TimeSpan endTime = TimeSpan.FromSeconds(chapterData.endTime);

                string startFormatted = startTime.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
                string endFormatted = endTime.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);


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
                return;
            }

            Console.Error.WriteLine("Chapter request failed");
        }
    }

    private static string FormatKey(byte[] keyBytes) =>
        BitConverter.ToString(keyBytes).Replace("-", "").ToLower();

    private static string BuildMp4DecryptKeyParam(byte[] keyId, byte[] key) =>
        $"--key {FormatKey(keyId)}:{FormatKey(key)}";

    private static string BuildShakaKeysParam(List<ContentKey> keys) =>
        "--enable_raw_key_decryption " + string.Join(" ",
            keys.Select(k => $"--keys key_id={FormatKey(k.KeyID)}:key={FormatKey(k.Bytes)}"));
}