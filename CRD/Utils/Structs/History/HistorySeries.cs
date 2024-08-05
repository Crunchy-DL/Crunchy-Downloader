using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.CustomList;
using Newtonsoft.Json;

namespace CRD.Utils.Structs.History;

public class HistorySeries : INotifyPropertyChanged{
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

    [JsonProperty("history_series_soft_subs_override")]
    public List<string> HistorySeriesSoftSubsOverride{ get; set; } =[];

    [JsonProperty("history_series_dub_lang_override")]
    public List<string> HistorySeriesDubLangOverride{ get; set; } =[];

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

    #region Language Override

    [JsonIgnore]
    public string SelectedSubs{ get; set; } = "";

    [JsonIgnore]
    public string SelectedDubs{ get; set; } = "";

    [JsonIgnore]
    public ObservableCollection<StringItem> SelectedSubLang{ get; set; } = new();

    [JsonIgnore]
    public ObservableCollection<StringItem> SelectedDubLang{ get; set; } = new();


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

    [JsonIgnore]
    public ObservableCollection<StringItem> DubLangList{ get; } = new(){
    };

    [JsonIgnore]
    public ObservableCollection<StringItem> SubLangList{ get; } = new(){
        new StringItem(){ stringValue = "all" },
        new StringItem(){ stringValue = "none" },
    };

    public void Init(){
        if (!(SubLangList.Count > 2 || DubLangList.Count > 0)){
            foreach (var languageItem in Languages.languages){
                SubLangList.Add(new StringItem{ stringValue = languageItem.CrLocale });
                DubLangList.Add(new StringItem{ stringValue = languageItem.CrLocale });
            }
        }

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
    }

    #endregion

    public async Task LoadImage(){
        if (IsImageLoaded || string.IsNullOrEmpty(ThumbnailImageUrl))
            return;

        try{
            using var client = new HttpClient();
            var response = await client.GetAsync(ThumbnailImageUrl);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            ThumbnailImage = new Bitmap(stream);
            IsImageLoaded = true;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailImage)));
        } catch (Exception ex){
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
        }
    }

    public void UpdateNewEpisodes(){
        int count = 0;
        bool foundWatched = false;
        var historyAddSpecials = CrunchyrollManager.Instance.CrunOptions.HistoryAddSpecials;
        var sonarrEnabled = CrunchyrollManager.Instance.CrunOptions.SonarrProperties != null && CrunchyrollManager.Instance.CrunOptions.SonarrProperties.SonarrEnabled;

        if (sonarrEnabled && CrunchyrollManager.Instance.CrunOptions.HistoryCountSonarr && !string.IsNullOrEmpty(SonarrSeriesId)){
            for (int i = Seasons.Count - 1; i >= 0; i--){
                var season = Seasons[i];

                var episodesList = season.EpisodesList;
                for (int j = episodesList.Count - 1; j >= 0; j--){
                    var episode = episodesList[j];

                    if (!string.IsNullOrEmpty(episode.SonarrEpisodeId) && !episode.SonarrHasFile){
                        count++;
                    }
                }
            }
        } else{
            for (int i = Seasons.Count - 1; i >= 0; i--){
                var season = Seasons[i];

                if (season.SpecialSeason == true){
                    if (historyAddSpecials){
                        var episodes = season.EpisodesList;
                        for (int j = episodes.Count - 1; j >= 0; j--){
                            if (!episodes[j].WasDownloaded){
                                count++;
                            }
                        }
                    }

                    continue;
                }

                var episodesList = season.EpisodesList;
                for (int j = episodesList.Count - 1; j >= 0; j--){
                    var episode = episodesList[j];

                    if (episode.SpecialEpisode){
                        if (historyAddSpecials && !episode.WasDownloaded){
                            count++;
                        }

                        continue;
                    }

                    if (!episode.WasDownloaded && !foundWatched){
                        count++;
                    } else{
                        foundWatched = true;
                        if (!historyAddSpecials){
                            break;
                        }
                    }
                }

                if (foundWatched && !historyAddSpecials){
                    break;
                }
            }
        }


        NewEpisodes = count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewEpisodes)));
    }

    public void SetFetchingData(){
        FetchingData = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
    }

    public async Task AddNewMissingToDownloads(){
        bool foundWatched = false;
        var historyAddSpecials = CrunchyrollManager.Instance.CrunOptions.HistoryAddSpecials;

        for (int i = Seasons.Count - 1; i >= 0; i--){
            var season = Seasons[i];

            if (season.SpecialSeason == true){
                if (historyAddSpecials){
                    var episodes = season.EpisodesList;
                    for (int j = episodes.Count - 1; j >= 0; j--){
                        if (!episodes[j].WasDownloaded){
                            await Seasons[i].EpisodesList[j].DownloadEpisode();
                        }
                    }
                }

                continue;
            }

            var episodesList = season.EpisodesList;
            for (int j = episodesList.Count - 1; j >= 0; j--){
                var episode = episodesList[j];

                if (episode.SpecialEpisode){
                    if (historyAddSpecials && !episode.WasDownloaded){
                        await Seasons[i].EpisodesList[j].DownloadEpisode();
                    }

                    continue;
                }

                if (!episode.WasDownloaded && !foundWatched){
                    await Seasons[i].EpisodesList[j].DownloadEpisode();
                } else{
                    foundWatched = true;
                    if (!historyAddSpecials){
                        break;
                    }
                }
            }

            if (foundWatched && !historyAddSpecials){
                break;
            }
        }
    }

    public async Task FetchData(string? seasonId){
        FetchingData = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
        await CrunchyrollManager.Instance.History.CRUpdateSeries(SeriesId, seasonId);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeriesTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeriesDescription)));
        CrunchyrollManager.Instance.History.MatchHistoryEpisodesWithSonarr(false, this);
        UpdateNewEpisodes();
        FetchingData = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
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
        Helpers.OpenUrl($"https://www.crunchyroll.com/series/{SeriesId}");
    }
}