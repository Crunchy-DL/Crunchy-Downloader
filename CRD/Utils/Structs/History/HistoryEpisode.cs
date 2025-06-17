using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Files;
using CRD.Utils.Sonarr.Models;
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
    
    [JsonProperty("episode_available_on_streaming_service")]
    public bool IsEpisodeAvailableOnStreamingService{ get; set; }
    
    [JsonProperty("episode_type")]
    public EpisodeType EpisodeType{ get; set; } = EpisodeType.Unknown;

    [JsonProperty("sonarr_episode_id")]
    public string? SonarrEpisodeId{ get; set; }

    [JsonProperty("sonarr_has_file")]
    public bool SonarrHasFile{ get; set; }
    
    [JsonProperty("sonarr_is_monitored")]
    public bool SonarrIsMonitored{ get; set; }

    [JsonProperty("sonarr_episode_number")]
    public string? SonarrEpisodeNumber{ get; set; }

    [JsonProperty("sonarr_season_number")]
    public string? SonarrSeasonNumber{ get; set; }

    [JsonProperty("sonarr_absolut_number")]
    public string? SonarrAbsolutNumber{ get; set; }

    [JsonIgnore]
    public string SonarrSeasonEpisodeText{
        get{
            if (int.TryParse(SonarrSeasonNumber, out int season) &&
                int.TryParse(SonarrEpisodeNumber, out int episode)){
                return $"S{season:D2}E{episode:D2}";
            }

            return $"S{SonarrSeasonNumber}E{SonarrEpisodeNumber}";
        }
    }
    
    [JsonProperty("history_episode_available_soft_subs")]
    public List<string> HistoryEpisodeAvailableSoftSubs{ get; set; } =[];

    [JsonProperty("history_episode_available_dub_lang")]
    public List<string> HistoryEpisodeAvailableDubLang{ get; set; } =[];

    [JsonIgnore]
    public string ReleaseDateFormated{
        get{
            if (!EpisodeCrPremiumAirDate.HasValue ||
                EpisodeCrPremiumAirDate.Value == DateTime.MinValue ||
                EpisodeCrPremiumAirDate.Value.Date == new DateTime(1970, 1, 1))
                return string.Empty;


            var cultureInfo = System.Globalization.CultureInfo.InvariantCulture;
            string monthAbbreviation = cultureInfo.DateTimeFormat.GetAbbreviatedMonthName(EpisodeCrPremiumAirDate.Value.Month);

            return string.Format("{0:00}.{1}.{2}", EpisodeCrPremiumAirDate.Value.Day, monthAbbreviation, EpisodeCrPremiumAirDate.Value.Year);
        }
    }
    
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
        switch (EpisodeType){
            case EpisodeType.MusicVideo:
                await QueueManager.Instance.CrAddMusicVideoToQueue(EpisodeId ?? string.Empty);
                break;
            case EpisodeType.Concert:
                await QueueManager.Instance.CrAddConcertToQueue(EpisodeId ?? string.Empty);
                break;
            case EpisodeType.Episode:
            case EpisodeType.Unknown:
            default:
                await QueueManager.Instance.CrAddEpisodeToQueue(EpisodeId ?? string.Empty,
                    string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.HistoryLang) ? CrunchyrollManager.Instance.DefaultLocale : CrunchyrollManager.Instance.CrunOptions.HistoryLang,
                    CrunchyrollManager.Instance.CrunOptions.DubLang, false, onlySubs);
                break;
        }
    }
    
    public void AssignSonarrEpisodeData(SonarrEpisode episode) {
        SonarrEpisodeId = episode.Id.ToString();
        SonarrEpisodeNumber = episode.EpisodeNumber.ToString();
        SonarrHasFile = episode.HasFile;
        SonarrIsMonitored = episode.Monitored;
        SonarrAbsolutNumber = episode.AbsoluteEpisodeNumber.ToString();
        SonarrSeasonNumber = episode.SeasonNumber.ToString();
        
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SonarrSeasonEpisodeText)));
    }
}