using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CRD.Downloader;
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

    [JsonIgnore]
    public Bitmap? ThumbnailImage{ get; set; }

    [JsonProperty("series_season_list")]
    public required RefreshableObservableCollection<HistorySeason> Seasons{ get; set; }

    [JsonProperty("series_download_path")]
    public string? SeriesDownloadPath{ get; set; }

    [JsonProperty("history_series_add_date")]
    public DateTime? HistorySeriesAddDate{ get; set; }
    
    public event PropertyChangedEventHandler? PropertyChanged;

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

    public async Task LoadImage(){
        try{
            using (var client = new HttpClient()){
                var response = await client.GetAsync(ThumbnailImageUrl);
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync()){
                    ThumbnailImage = new Bitmap(stream);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailImage)));
                }
            }
        } catch (Exception ex){
            // Handle exceptions
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
        }
    }

    public void UpdateNewEpisodes(){
        int count = 0;
        bool foundWatched = false;

        // Iterate over the Seasons list from the end to the beginning
        for (int i = Seasons.Count - 1; i >= 0 && !foundWatched; i--){
            if (Seasons[i].SpecialSeason == true){
                continue;
            }

            // Iterate over the Episodes from the end to the beginning
            for (int j = Seasons[i].EpisodesList.Count - 1; j >= 0 && !foundWatched; j--){
                if (Seasons[i].EpisodesList[j].SpecialEpisode){
                    continue;
                }

                if (!Seasons[i].EpisodesList[j].WasDownloaded){
                    count++;
                } else{
                    foundWatched = true;
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

        // Iterate over the Seasons list from the end to the beginning
        for (int i = Seasons.Count - 1; i >= 0 && !foundWatched; i--){
            if (Seasons[i].SpecialSeason == true){
                continue;
            }

            // Iterate over the Episodes from the end to the beginning
            for (int j = Seasons[i].EpisodesList.Count - 1; j >= 0 && !foundWatched; j--){
                if (Seasons[i].EpisodesList[j].SpecialEpisode){
                    continue;
                }

                if (!Seasons[i].EpisodesList[j].WasDownloaded){
                    //ADD to download queue
                    await Seasons[i].EpisodesList[j].DownloadEpisode();
                } else{
                    foundWatched = true;
                }
            }
        }
    }

    public async Task FetchData(string? seasonId){
        FetchingData = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
        await Crunchyroll.Instance.CrHistory.UpdateSeries(SeriesId, seasonId);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeriesTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeriesDescription)));
        Crunchyroll.Instance.CrHistory.MatchHistoryEpisodesWithSonarr(false, this);
        UpdateNewEpisodes();
        FetchingData = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
    }

    public void RemoveSeason(string? season){
        HistorySeason? objectToRemove = Seasons.FirstOrDefault(se => se.SeasonId == season) ?? null;
        if (objectToRemove != null){
            Seasons.Remove(objectToRemove);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Seasons)));
        }

        CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
    }

    public void OpenSonarrPage(){
        var sonarrProp = Crunchyroll.Instance.CrunOptions.SonarrProperties;

        if (sonarrProp == null) return;

        Helpers.OpenUrl($"http{(sonarrProp.UseSsl ? "s" : "")}://{sonarrProp.Host}:{sonarrProp.Port}{(sonarrProp.UrlBase ?? "")}/series/{SonarrSlugTitle}");
    }

    public void OpenCrPage(){
        Helpers.OpenUrl($"https://www.crunchyroll.com/series/{SeriesId}");
    }
}