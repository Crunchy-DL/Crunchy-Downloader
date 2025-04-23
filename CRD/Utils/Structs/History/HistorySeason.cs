using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CRD.Utils.Files;
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

    [JsonProperty("season_downloaded_episodes")]
    public int DownloadedEpisodes{ get; set; }

    [JsonProperty("season_episode_list")]
    public required List<HistoryEpisode> EpisodesList{ get; set; }

    [JsonProperty("series_download_path")]
    public string? SeasonDownloadPath{ get; set; }

    [JsonProperty("history_season_video_quality_override")]
    public string HistorySeasonVideoQualityOverride{ get; set; } = "";

    [JsonProperty("history_season_soft_subs_override")]
    public ObservableCollection<string> HistorySeasonSoftSubsOverride{ get; set; } =[];

    [JsonProperty("history_season_dub_lang_override")]
    public ObservableCollection<string> HistorySeasonDubLangOverride{ get; set; } =[];

    [JsonIgnore]
    public string CombinedProperty => SpecialSeason ?? false ? $"Specials {SeasonNum}" : $"Season {SeasonNum}";

    [JsonIgnore]
    public bool IsExpanded{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Settings Override

    [JsonIgnore]
    public StringItem? _selectedVideoQualityItem;

    [JsonIgnore]
    private bool Loading = false;

    [JsonIgnore]
    public StringItem? SelectedVideoQualityItem{
        get => _selectedVideoQualityItem;
        set{
            _selectedVideoQualityItem = value;

            HistorySeasonVideoQualityOverride = value?.stringValue ?? "";
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
        HistorySeasonSoftSubsOverride.Clear();
        HistorySeasonDubLangOverride.Clear();

        if (SelectedSubLang.Count != 0){
            for (var i = 0; i < SelectedSubLang.Count; i++){
                HistorySeasonSoftSubsOverride.Add(SelectedSubLang[i].stringValue);
            }
        }

        if (SelectedDubLang.Count != 0){
            for (var i = 0; i < SelectedDubLang.Count; i++){
                HistorySeasonDubLangOverride.Add(SelectedDubLang[i].stringValue);
            }
        }

        SelectedDubs = string.Join(", ", HistorySeasonDubLangOverride) ?? "";
        SelectedSubs = string.Join(", ", HistorySeasonSoftSubsOverride) ?? "";


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

        SelectedVideoQualityItem = VideoQualityList.FirstOrDefault(a => HistorySeasonVideoQualityOverride.Equals(a.stringValue)) ?? new StringItem(){ stringValue = "" };

        var softSubLang = SubLangList.Where(a => HistorySeasonSoftSubsOverride.Contains(a.stringValue)).ToList();
        var dubLang = DubLangList.Where(a => HistorySeasonDubLangOverride.Contains(a.stringValue)).ToList();

        SelectedSubLang.Clear();
        foreach (var listBoxItem in softSubLang){
            SelectedSubLang.Add(listBoxItem);
        }

        SelectedDubLang.Clear();
        foreach (var listBoxItem in dubLang){
            SelectedDubLang.Add(listBoxItem);
        }

        SelectedDubs = string.Join(", ", HistorySeasonDubLangOverride) ?? "";
        SelectedSubs = string.Join(", ", HistorySeasonSoftSubsOverride) ?? "";

        SelectedSubLang.CollectionChanged += Changes;
        SelectedDubLang.CollectionChanged += Changes;
        Loading = false;
    }

    #endregion

    public void UpdateDownloaded(string? EpisodeId){
        if (!string.IsNullOrEmpty(EpisodeId)){
            var episode = EpisodesList.First(e => e.EpisodeId == EpisodeId);
            episode.ToggleWasDownloaded();
        }

        DownloadedEpisodes = EpisodesList.FindAll(e => e.WasDownloaded).Count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedEpisodes)));
        CfgManager.UpdateHistoryFile();
    }

    public void UpdateDownloaded(){
        DownloadedEpisodes = EpisodesList.FindAll(e => e.WasDownloaded).Count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedEpisodes)));
        CfgManager.UpdateHistoryFile();
    }

    public void UpdateDownloadedSilent(){
        DownloadedEpisodes = EpisodesList.FindAll(e => e.WasDownloaded).Count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedEpisodes)));
    }
}