using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CRD.Downloader;
using Newtonsoft.Json;

namespace CRD.Utils.Structs.History;

public class HistorySeason : INotifyPropertyChanged{
    [JsonProperty("season_title")]
    public string? SeasonTitle{ get; set; }

    [JsonProperty("season_id")]
    public string? SeasonId{ get; set; }

    [JsonProperty("season_cr_season_number")]
    public string? SeasonNum{ get; set; }

    [JsonProperty("season_special_season")]
    public bool? SpecialSeason{ get; set; }

    [JsonIgnore]
    public string CombinedProperty => SpecialSeason ?? false ? $"Specials {SeasonNum}" : $"Season {SeasonNum}";

    [JsonProperty("season_downloaded_episodes")]
    public int DownloadedEpisodes{ get; set; }

    [JsonProperty("season_episode_list")]
    public required List<HistoryEpisode> EpisodesList{ get; set; }

    [JsonProperty("series_download_path")]
    public string? SeasonDownloadPath{ get; set; }
    
    [JsonIgnore]
    public bool IsExpanded{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateDownloaded(string? EpisodeId){
        if (!string.IsNullOrEmpty(EpisodeId)){
            EpisodesList.First(e => e.EpisodeId == EpisodeId).ToggleWasDownloaded();
        }

        DownloadedEpisodes = EpisodesList.FindAll(e => e.WasDownloaded).Count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedEpisodes)));
        CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
    }

    public void UpdateDownloaded(){
        DownloadedEpisodes = EpisodesList.FindAll(e => e.WasDownloaded).Count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedEpisodes)));
        CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
    }
    
    public void UpdateDownloadedSilent(){
        DownloadedEpisodes = EpisodesList.FindAll(e => e.WasDownloaded).Count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedEpisodes)));
    }
    
}

public class UpdateDownloadedHistorySeason{
    public string? EpisodeId;
    public HistorySeries? HistorySeries;
}
