using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using Newtonsoft.Json;

namespace CRD.Utils.Structs.History;

public class HistoryEpisode : INotifyPropertyChanged{
    [JsonProperty("episode_title")]
    public string? EpisodeTitle{ get; set; }

    [JsonProperty("episode_id")]
    public string? EpisodeId{ get; set; }

    [JsonProperty("episode_cr_episode_number")]
    public string? Episode{ get; set; }

    [JsonProperty("episode_cr_episode_description")]
    public string? EpisodeDescription{ get; set; }

    [JsonProperty("episode_cr_season_number")]
    public string? EpisodeSeasonNum{ get; set; }

    [JsonProperty("episode_cr_premium_air_date")]
    public DateTime? EpisodeCrPremiumAirDate{ get; set; }

    [JsonProperty("episode_was_downloaded")]
    public bool WasDownloaded{ get; set; }

    [JsonProperty("episode_special_episode")]
    public bool SpecialEpisode{ get; set; }

    [JsonProperty("sonarr_episode_id")]
    public string? SonarrEpisodeId{ get; set; }

    [JsonProperty("sonarr_has_file")]
    public bool SonarrHasFile{ get; set; }

    [JsonProperty("sonarr_episode_number")]
    public string? SonarrEpisodeNumber{ get; set; }

    [JsonProperty("sonarr_season_number")]
    public string? SonarrSeasonNumber{ get; set; }

    [JsonProperty("sonarr_absolut_number")]
    public string? SonarrAbsolutNumber{ get; set; }

    [JsonProperty("history_episode_available_soft_subs")]
    public List<string> HistoryEpisodeAvailableSoftSubs{ get; set; } =[];

    [JsonProperty("history_episode_available_dub_lang")]
    public List<string> HistoryEpisodeAvailableDubLang{ get; set; } =[];

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ToggleWasDownloaded(){
        WasDownloaded = !WasDownloaded;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WasDownloaded)));
    }

    public void ToggleWasDownloadedSeries(HistorySeries? series){
        WasDownloaded = !WasDownloaded;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WasDownloaded)));

        if (series?.Seasons != null){
            foreach (var historySeason in series.Seasons){
                historySeason.UpdateDownloadedSilent();
            }

            series.UpdateNewEpisodes();
        }

        CfgManager.UpdateHistoryFile();
    }

    public async Task DownloadEpisode(bool onlySubs = false){
        await QueueManager.Instance.CrAddEpisodeToQueue(EpisodeId,
            string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.HistoryLang) ? CrunchyrollManager.Instance.DefaultLocale : CrunchyrollManager.Instance.CrunOptions.HistoryLang,
            CrunchyrollManager.Instance.CrunOptions.DubLang, false, onlySubs);
    }
}