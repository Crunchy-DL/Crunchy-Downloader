using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.CustomList;
using CRD.Utils.Files;
using CRD.Views;
using Newtonsoft.Json;
using ReactiveUI;

namespace CRD.Utils.Structs.History;

public class HistorySeries : INotifyPropertyChanged{
    [JsonProperty("series_streaming_service")]
    public StreamingService SeriesStreamingService{ get; set; } = StreamingService.Unknown;

    [JsonProperty("series_type")]
    public SeriesType SeriesType{ get; set; } = SeriesType.Unknown;

    [JsonProperty("series_is_inactive")]
    public bool IsInactive{ get; set; }

    [JsonProperty("series_title")]
    public string? SeriesTitle{ get; set; }

    [JsonProperty("series_id")]
    public string? SeriesId{ get; set; }

    [JsonProperty("sonarr_series_id")]
    public string? SonarrSeriesId{ get; set; }

    [JsonProperty("sonarr_tvdb_id")]
    public string? SonarrTvDbId{ get; set; }

    [JsonProperty("sonarr_slug_title")]
    public string? SonarrSlugTitle{ get; set; }

    [JsonProperty("sonarr_next_air_date")]
    public string? SonarrNextAirDate{ get; set; }

    [JsonProperty("series_description")]
    public string? SeriesDescription{ get; set; }

    [JsonProperty("series_thumbnail_url")]
    public string? ThumbnailImageUrl{ get; set; }

    [JsonProperty("series_new_episodes")]
    public int NewEpisodes{ get; set; }

    [JsonProperty("series_season_list")]
    public required RefreshableObservableCollection<HistorySeason> Seasons{ get; set; }

    [JsonProperty("series_download_path")]
    public string? SeriesDownloadPath{ get; set; }

    [JsonProperty("history_series_add_date")]
    public DateTime? HistorySeriesAddDate{ get; set; }

    [JsonProperty("history_series_video_quality_override")]
    public string HistorySeriesVideoQualityOverride{ get; set; } = "";

    [JsonProperty("history_series_available_soft_subs")]
    public List<string> HistorySeriesAvailableSoftSubs{ get; set; } =[];

    [JsonProperty("history_series_available_dub_lang")]
    public List<string> HistorySeriesAvailableDubLang{ get; set; } =[];

    [JsonProperty("history_series_soft_subs_override")]
    public ObservableCollection<string> HistorySeriesSoftSubsOverride{ get; set; } =[];

    [JsonProperty("history_series_dub_lang_override")]
    public ObservableCollection<string> HistorySeriesDubLangOverride{ get; set; } =[];

    public event PropertyChangedEventHandler? PropertyChanged;

    [JsonIgnore]
    public Bitmap? ThumbnailImage{ get; set; }

    [JsonIgnore]
    public bool IsImageLoaded{ get; private set; } = false;

    [JsonIgnore]
    public bool FetchingData{ get; set; }

    [JsonIgnore]
    public bool IsExpanded{ get; set; }

    [JsonIgnore]
    public bool EditModeEnabled{
        get => _editModeEnabled;
        set{
            if (_editModeEnabled != value){
                _editModeEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditModeEnabled)));
            }
        }
    }

    [JsonIgnore]
    private bool _editModeEnabled;

    [JsonIgnore]
    public string SeriesFolderPath{ get; set; }

    [JsonIgnore]
    public bool SeriesFolderPathExists{ get; set; }

    #region Settings Override

    [JsonIgnore]
    private bool Loading = false;

    [JsonIgnore]
    public StringItem? _selectedVideoQualityItem;

    [JsonIgnore]
    public StringItem? SelectedVideoQualityItem{
        get => _selectedVideoQualityItem;
        set{
            _selectedVideoQualityItem = value;

            HistorySeriesVideoQualityOverride = value?.stringValue ?? "";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedVideoQualityItem)));
            if (!Loading){
                CfgManager.UpdateHistoryFile();
            }
        }
    }

    [JsonIgnore]
    public string SelectedSubs{ get; set; } = "";

    [JsonIgnore]
    public string SelectedDubs{ get; set; } = "";

    [JsonIgnore]
    public ObservableCollection<StringItem> SelectedSubLang{ get; set; } = new();

    [JsonIgnore]
    public ObservableCollection<StringItem> SelectedDubLang{ get; set; } = new();


    [JsonIgnore]
    public ObservableCollection<StringItem> DubLangList{ get; } = new(){
    };

    [JsonIgnore]
    public ObservableCollection<StringItem> SubLangList{ get; } = new(){
        new StringItem(){ stringValue = "all" },
        new StringItem(){ stringValue = "none" },
    };

    [JsonIgnore]
    public ObservableCollection<StringItem> VideoQualityList{ get; } = new(){
        new StringItem(){ stringValue = "best" },
        new StringItem(){ stringValue = "1080p" },
        new StringItem(){ stringValue = "720p" },
        new StringItem(){ stringValue = "480p" },
        new StringItem(){ stringValue = "360p" },
        new StringItem(){ stringValue = "240p" },
        new StringItem(){ stringValue = "worst" },
    };

    private void UpdateSubAndDubString(){
        HistorySeriesSoftSubsOverride.Clear();
        HistorySeriesDubLangOverride.Clear();

        if (SelectedSubLang.Count != 0){
            for (var i = 0; i < SelectedSubLang.Count; i++){
                HistorySeriesSoftSubsOverride.Add(SelectedSubLang[i].stringValue);
            }
        }

        if (SelectedDubLang.Count != 0){
            for (var i = 0; i < SelectedDubLang.Count; i++){
                HistorySeriesDubLangOverride.Add(SelectedDubLang[i].stringValue);
            }
        }

        SelectedDubs = string.Join(", ", HistorySeriesDubLangOverride) ?? "";
        SelectedSubs = string.Join(", ", HistorySeriesSoftSubsOverride) ?? "";

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSubs)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDubs)));

        CfgManager.UpdateHistoryFile();
    }

    private void Changes(object? sender, NotifyCollectionChangedEventArgs e){
        UpdateSubAndDubString();
    }

    public void Init(){
        Loading = true;
        if (!(SubLangList.Count > 2 || DubLangList.Count > 0)){
            foreach (var languageItem in Languages.languages){
                SubLangList.Add(new StringItem{ stringValue = languageItem.CrLocale });
                DubLangList.Add(new StringItem{ stringValue = languageItem.CrLocale });
            }
        }

        SelectedVideoQualityItem = VideoQualityList.FirstOrDefault(a => HistorySeriesVideoQualityOverride.Equals(a.stringValue)) ?? new StringItem(){ stringValue = "" };

        var softSubLang = SubLangList.Where(a => HistorySeriesSoftSubsOverride.Contains(a.stringValue)).ToList();
        var dubLang = DubLangList.Where(a => HistorySeriesDubLangOverride.Contains(a.stringValue)).ToList();

        SelectedSubLang.Clear();
        foreach (var listBoxItem in softSubLang){
            SelectedSubLang.Add(listBoxItem);
        }

        SelectedDubLang.Clear();
        foreach (var listBoxItem in dubLang){
            SelectedDubLang.Add(listBoxItem);
        }

        SelectedDubs = string.Join(", ", HistorySeriesDubLangOverride) ?? "";
        SelectedSubs = string.Join(", ", HistorySeriesSoftSubsOverride) ?? "";

        SelectedSubLang.CollectionChanged += Changes;
        SelectedDubLang.CollectionChanged += Changes;

        UpdateSeriesFolderPath();

        Loading = false;
    }

    #endregion

    public async Task LoadImage(){
        if (IsImageLoaded || string.IsNullOrEmpty(ThumbnailImageUrl))
            return;

        try{
            ThumbnailImage = await Helpers.LoadImage(ThumbnailImageUrl);
            IsImageLoaded = true;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailImage)));
        } catch (Exception ex){
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
        }
    }

    public void UpdateNewEpisodes(){
        int count = 0;

        bool foundWatched = false;
        var options = CrunchyrollManager.Instance.CrunOptions;

        bool historyAddSpecials = options.HistoryAddSpecials;
        bool sonarrEnabled = SeriesType != SeriesType.Artist &&
                             options.SonarrProperties?.SonarrEnabled == true &&
                             !string.IsNullOrEmpty(SonarrSeriesId);
        bool skipUnmonitored = options.HistorySkipUnmonitored;
        bool countMissing = options.HistoryCountMissing;
        bool useSonarrCounting = options.HistoryCountSonarr;

        for (int i = Seasons.Count - 1; i >= 0; i--){
            var season = Seasons[i];
            var episodes = season.EpisodesList;

            if (season.SpecialSeason == true){
                if (historyAddSpecials){
                    for (int j = episodes.Count - 1; j >= 0; j--){
                        var ep = episodes[j];

                        if (skipUnmonitored && sonarrEnabled && !ep.SonarrIsMonitored){
                            continue;
                        }

                        if (ShouldCountEpisode(ep, sonarrEnabled && useSonarrCounting, countMissing, false)){
                            count++;
                        }
                    }
                }

                continue;
            }


            for (int j = episodes.Count - 1; j >= 0; j--){
                var ep = episodes[j];

                if (skipUnmonitored && sonarrEnabled && !ep.SonarrIsMonitored){
                    continue;
                }

                if (ep.SpecialEpisode){
                    if (historyAddSpecials && ShouldCountEpisode(ep, sonarrEnabled && useSonarrCounting, countMissing, false)){
                        count++;
                    }

                    continue;
                }

                if (ShouldCountEpisode(ep, sonarrEnabled && useSonarrCounting, countMissing, foundWatched)){
                    count++;
                } else{
                    foundWatched = true;
                    //if not count specials break
                    if (!historyAddSpecials && !countMissing){
                        break;
                    }
                }
            }

            if (foundWatched && !historyAddSpecials && !countMissing){
                break;
            }
        }

        NewEpisodes = count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewEpisodes)));
    }

    private bool ShouldCountEpisode(HistoryEpisode episode, bool useSonarr, bool countMissing, bool foundWatched){
        if (useSonarr)
            return !string.IsNullOrEmpty(episode.SonarrEpisodeId) && !episode.SonarrHasFile;

        return !episode.WasDownloaded && (!foundWatched || countMissing);
    }

    public void SetFetchingData(){
        FetchingData = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
    }

    public async Task AddNewMissingToDownloads(){
        bool foundWatched = false;
        var options = CrunchyrollManager.Instance.CrunOptions;

        bool historyAddSpecials = options.HistoryAddSpecials;
        bool sonarrEnabled = SeriesType != SeriesType.Artist &&
                             options.SonarrProperties?.SonarrEnabled == true &&
                             !string.IsNullOrEmpty(SonarrSeriesId);
        bool skipUnmonitored = options.HistorySkipUnmonitored;
        bool countMissing = options.HistoryCountMissing;
        bool useSonarrCounting = options.HistoryCountSonarr;

        for (int i = Seasons.Count - 1; i >= 0; i--){
            var season = Seasons[i];
            var episodes = season.EpisodesList;

            if (season.SpecialSeason == true){
                if (historyAddSpecials){
                    for (int j = episodes.Count - 1; j >= 0; j--){
                        var ep = episodes[j];

                        if (skipUnmonitored && sonarrEnabled && !ep.SonarrIsMonitored){
                            continue;
                        }

                        if (ShouldCountEpisode(ep, sonarrEnabled && useSonarrCounting, countMissing, false)){
                            await ep.DownloadEpisode();
                        }
                    }
                }

                continue;
            }

            for (int j = episodes.Count - 1; j >= 0; j--){
                var ep = episodes[j];

                if (skipUnmonitored && sonarrEnabled && !ep.SonarrIsMonitored){
                    continue;
                }

                if (ep.SpecialEpisode){
                    if (historyAddSpecials && ShouldCountEpisode(ep, sonarrEnabled && useSonarrCounting, countMissing, false)){
                        await ep.DownloadEpisode();
                    }

                    continue;
                }

                if (ShouldCountEpisode(ep, sonarrEnabled && useSonarrCounting, countMissing, foundWatched)){
                    await ep.DownloadEpisode();
                } else{
                    foundWatched = true;
                    if (!historyAddSpecials && !countMissing){
                        break;
                    }
                }
            }

            if (foundWatched && !historyAddSpecials && !countMissing){
                break;
            }
        }
    }

    public async Task<bool> FetchData(string? seasonId){
        Console.WriteLine($"Fetching Data for: {SeriesTitle}");
        FetchingData = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
        var isOk = true;

        switch (SeriesType){
            case SeriesType.Artist:
                try{
                    await CrunchyrollManager.Instance.CrMusic.ParseArtistVideosByIdAsync(SeriesId,
                        string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.HistoryLang) ? CrunchyrollManager.Instance.DefaultLocale : CrunchyrollManager.Instance.CrunOptions.HistoryLang, true, true);
                } catch (Exception e){
                    isOk = false;
                    Console.Error.WriteLine("Failed to update History artist");
                    Console.Error.WriteLine(e);
                }

                break;
            case SeriesType.Series:
            case SeriesType.Unknown:
            default:
                try{
                    isOk = await CrunchyrollManager.Instance.History.CrUpdateSeries(SeriesId, seasonId);
                } catch (Exception e){
                    isOk = false;
                    Console.Error.WriteLine("Failed to update History series");
                    Console.Error.WriteLine(e);
                }

                break;
        }


        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeriesTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeriesDescription)));
        UpdateNewEpisodes();
        FetchingData = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));

        return isOk;
    }

    public void RemoveSeason(string? season){
        HistorySeason? objectToRemove = Seasons.FirstOrDefault(se => se.SeasonId == season) ?? null;
        if (objectToRemove != null){
            Seasons.Remove(objectToRemove);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Seasons)));
            CfgManager.UpdateHistoryFile();
        }
    }

    public void OpenSonarrPage(){
        var sonarrProp = CrunchyrollManager.Instance.CrunOptions.SonarrProperties;

        if (sonarrProp == null) return;

        Helpers.OpenUrl($"http{(sonarrProp.UseSsl ? "s" : "")}://{sonarrProp.Host}:{sonarrProp.Port}{(sonarrProp.UrlBase ?? "")}/series/{SonarrSlugTitle}");
    }

    public void OpenCrPage(){
        switch (SeriesType){
            case SeriesType.Artist:
                Helpers.OpenUrl($"https://www.crunchyroll.com/artist/{SeriesId}");
                break;
            case SeriesType.Series:
            case SeriesType.Unknown:
            default:
                Helpers.OpenUrl($"https://www.crunchyroll.com/series/{SeriesId}");
                break;
        }
    }

    public void UpdateSeriesFolderPath(){
        var season = Seasons.FirstOrDefault(season => !string.IsNullOrEmpty(season.SeasonDownloadPath));

        if (!string.IsNullOrEmpty(SeriesDownloadPath) && Directory.Exists(SeriesDownloadPath)){
            SeriesFolderPath = SeriesDownloadPath;
            SeriesFolderPathExists = true;
        }

        if (season is{ SeasonDownloadPath: not null }){
            try{
                var seasonPath = season.SeasonDownloadPath;
                var directoryInfo = new DirectoryInfo(seasonPath);

                if (!string.IsNullOrEmpty(directoryInfo.Parent?.FullName)){
                    string parentFolderPath = directoryInfo.Parent?.FullName ?? string.Empty;

                    if (Directory.Exists(parentFolderPath)){
                        SeriesFolderPath = parentFolderPath;
                        SeriesFolderPathExists = true;
                    }
                }
            } catch (Exception e){
                Console.Error.WriteLine($"An error occurred while opening the folder: {e.Message}");
            }
        } else{
            string customPath;

            if (string.IsNullOrEmpty(SeriesTitle))
                return;

            var seriesTitle = FileNameManager.CleanupFilename(SeriesTitle);

            if (string.IsNullOrEmpty(seriesTitle))
                return;

            // Check Crunchyroll download directory
            var downloadDirPath = CrunchyrollManager.Instance.CrunOptions.DownloadDirPath;
            if (!string.IsNullOrEmpty(downloadDirPath)){
                customPath = Path.Combine(downloadDirPath, seriesTitle);
            } else{
                // Fallback to configured VIDEOS_DIR path
                customPath = Path.Combine(CfgManager.PathVIDEOS_DIR, seriesTitle);
            }

            // Check if custom path exists
            if (Directory.Exists(customPath)){
                SeriesFolderPath = customPath;
                SeriesFolderPathExists = true;
            }
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeriesFolderPathExists)));
    }
}