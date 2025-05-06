using System.Collections.Generic;
using CRD.Utils.Sonarr;
using CRD.ViewModels;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace CRD.Utils.Structs.Crunchyroll;

public class CrDownloadOptions{
    #region General Settings

    [JsonProperty("auto_download")]
    public bool AutoDownload{ get; set; }

    [JsonProperty("remove_finished_downloads")]
    public bool RemoveFinishedDownload{ get; set; }

    [JsonIgnore]
    public int Timeout{ get; set; }

    [JsonProperty("retry_delay")]
    public int RetryDelay{ get; set; }
    
    [JsonProperty("retry_attempts")]
    public int RetryAttempts{ get; set; }

    [JsonIgnore]
    public string Force{ get; set; } = "";

    [JsonProperty("simultaneous_downloads")]
    public int SimultaneousDownloads{ get; set; }

    [JsonProperty("theme")]
    public string Theme{ get; set; } = "";

    [JsonProperty("accent_color")]
    public string? AccentColor{ get; set; }

    [JsonProperty("background_image_path")]
    public string? BackgroundImagePath{ get; set; }

    [JsonProperty("download_finished_play_sound")]
    public bool DownloadFinishedPlaySound{ get; set; }
    
    [JsonProperty("download_finished_sound_path")]
    public string? DownloadFinishedSoundPath{ get; set; }
    
    
    [JsonProperty("background_image_opacity")]
    public double BackgroundImageOpacity{ get; set; }

    [JsonProperty("background_image_blur_radius")]
    public double BackgroundImageBlurRadius{ get; set; }

    [JsonIgnore]
    public List<string> Override{ get; set; } =[];

    [JsonIgnore]
    public string CcTag{ get; set; } = "CC";

    [JsonIgnore]
    public bool Nocleanup{ get; set; }

    [JsonProperty("history")]
    public bool History{ get; set; }
    
    [JsonProperty("history_count_missing")]
    public bool HistoryCountMissing { get; set; }

    [JsonProperty("history_include_cr_artists")]
    public bool HistoryIncludeCrArtists{ get; set; }

    [JsonProperty("history_lang")]
    public string? HistoryLang{ get; set; }

    [JsonProperty("history_add_specials")]
    public bool HistoryAddSpecials{ get; set; }

    [JsonProperty("history_skip_unmonitored")]
    public bool HistorySkipUnmonitored{ get; set; }

    [JsonProperty("history_count_sonarr")]
    public bool HistoryCountSonarr{ get; set; }

    [JsonProperty("sonarr_properties")]
    public SonarrProperties? SonarrProperties{ get; set; }

    [JsonProperty("log_mode")]
    public bool LogMode{ get; set; }

    [JsonProperty("download_dir_path")]
    public string? DownloadDirPath{ get; set; }

    [JsonProperty("download_temp_dir_path")]
    public string? DownloadTempDirPath{ get; set; }

    [JsonProperty("download_to_temp_folder")]
    public bool DownloadToTempFolder{ get; set; }

    [JsonProperty("history_page_properties")]
    public HistoryPageProperties? HistoryPageProperties{ get; set; }

    [JsonProperty("seasons_page_properties")]
    public SeasonsPageProperties? SeasonsPageProperties{ get; set; }

    [JsonProperty("download_speed_limit")]
    public int DownloadSpeedLimit{ get; set; }

    [JsonProperty("proxy_enabled")]
    public bool ProxyEnabled{ get; set; }

    [JsonProperty("proxy_socks")]
    public bool ProxySocks{ get; set; }

    [JsonProperty("proxy_host")]
    public string? ProxyHost{ get; set; }

    [JsonProperty("proxy_port")]
    public int ProxyPort{ get; set; }

    [JsonProperty("proxy_username")]
    public string? ProxyUsername{ get; set; }

    [JsonProperty("proxy_password")]
    public string? ProxyPassword{ get; set; }

    #endregion

    #region Crunchyroll Settings

    [JsonProperty("cr_mark_as_watched")]
    public bool MarkAsWatched{ get; set; }

    [JsonProperty("cr_beta_api")]
    public bool UseCrBetaApi{ get; set; }

    [JsonProperty("hard_sub_lang")]
    public string Hslang{ get; set; } = "";

    [JsonIgnore]
    public int Kstream{ get; set; }

    [JsonProperty("no_video")]
    public bool Novids{ get; set; }

    [JsonProperty("no_audio")]
    public bool Noaudio{ get; set; }

    [JsonIgnore]
    public int StreamServer{ get; set; }

    [JsonProperty("quality_video")]
    public string QualityVideo{ get; set; } = "";

    [JsonProperty("quality_audio")]
    public string QualityAudio{ get; set; } = "";

    [JsonProperty("file_name")]
    public string FileName{ get; set; } = "";

    [JsonProperty("leading_numbers")]
    public int Numbers{ get; set; }

    [JsonProperty("download_part_size")]
    public int Partsize{ get; set; }

    [JsonProperty("soft_subs")]
    public List<string> DlSubs{ get; set; } =[];

    [JsonIgnore]
    public bool SkipSubs{ get; set; }

    [JsonProperty("mux_skip_subs")]
    public bool SkipSubsMux{ get; set; }

    [JsonProperty("subs_add_scaled_border")]
    public ScaledBorderAndShadowSelection SubsAddScaledBorder{ get; set; }

    [JsonProperty("include_signs_subs")]
    public bool IncludeSignsSubs{ get; set; }

    [JsonProperty("mux_signs_subs_flag")]
    public bool SignsSubsAsForced{ get; set; }

    [JsonProperty("include_cc_subs")]
    public bool IncludeCcSubs{ get; set; }

    [JsonProperty("cc_subs_font")]
    public string? CcSubsFont{ get; set; }

    [JsonProperty("mux_cc_subs_flag")]
    public bool CcSubsMuxingFlag{ get; set; }

    [JsonProperty("mux_mp4")]
    public bool Mp4{ get; set; }
    
    [JsonProperty("mux_fonts")]
    public bool MuxFonts{ get; set; }

    [JsonProperty("mux_video_title")]
    public string? VideoTitle{ get; set; }

    [JsonProperty("mux_video_description")]
    public bool IncludeVideoDescription{ get; set; }

    [JsonProperty("mux_description_lang")]
    public string? DescriptionLang{ get; set; }

    [JsonProperty("mux_ffmpeg")]
    public List<string> FfmpegOptions{ get; set; } =[];

    [JsonProperty("mux_mkvmerge")]
    public List<string> MkvmergeOptions{ get; set; } =[];

    [JsonProperty("mux_default_sub")]
    public string DefaultSub{ get; set; } = "";

    [JsonProperty("mux_default_sub_signs")]
    public bool DefaultSubSigns{ get; set; }

    [JsonProperty("mux_default_sub_forced_display")]
    public bool DefaultSubForcedDisplay{ get; set; }

    [JsonProperty("mux_default_dub")]
    public string DefaultAudio{ get; set; } = "";

    [JsonProperty("dl_video_once")]
    public bool DlVideoOnce{ get; set; }

    [JsonProperty("keep_dubs_seperate")]
    public bool KeepDubsSeperate{ get; set; }

    [JsonProperty("dl_first_available_dub")]
    public bool DownloadFirstAvailableDub{ get; set; }

    [JsonProperty("mux_skip_muxing")]
    public bool SkipMuxing{ get; set; }

    [JsonProperty("mux_sync_dubs")]
    public bool SyncTiming{ get; set; }
    
    [JsonProperty("mux_sync_hwaccel")]
    public string? FfmpegHwAccelFlag{ get; set; }

    [JsonProperty("encode_enabled")]
    public bool IsEncodeEnabled{ get; set; }

    [JsonProperty("encode_preset")]
    public string? EncodingPresetName{ get; set; }

    [JsonProperty("chapters")]
    public bool Chapters{ get; set; }

    [JsonProperty("dub_lang")]
    public List<string> DubLang{ get; set; } =[];

    [JsonProperty("calendar_language")]
    public string? SelectedCalendarLanguage{ get; set; }

    [JsonProperty("calendar_dub_filter")]
    public string? CalendarDubFilter{ get; set; }

    [JsonProperty("calendar_custom")]
    public bool CustomCalendar{ get; set; }

    [JsonProperty("calendar_hide_dubs")]
    public bool CalendarHideDubs{ get; set; }

    [JsonProperty("calendar_filter_by_air_date")]
    public bool CalendarFilterByAirDate{ get; set; }

    [JsonProperty("calendar_show_upcoming_episodes")]
    public bool CalendarShowUpcomingEpisodes{ get; set; }

    [JsonProperty("stream_endpoint")]
    public string? StreamEndpoint{ get; set; }

    [JsonProperty("search_fetch_featured_music")]
    public bool SearchFetchFeaturedMusic{ get; set; }

    #endregion
}

public class CrDownloadOptionsYaml{
    #region General Settings

    [YamlMember(Alias = "auto_download", ApplyNamingConventions = false)]
    public bool AutoDownload{ get; set; }

    [YamlMember(Alias = "remove_finished_downloads", ApplyNamingConventions = false)]
    public bool RemoveFinishedDownload{ get; set; }

    [YamlIgnore]
    public int Timeout{ get; set; }

    [YamlIgnore]
    public int FsRetryTime{ get; set; }

    [YamlIgnore]
    public string Force{ get; set; } = "";

    [YamlMember(Alias = "simultaneous_downloads", ApplyNamingConventions = false)]
    public int SimultaneousDownloads{ get; set; }

    [YamlMember(Alias = "theme", ApplyNamingConventions = false)]
    public string Theme{ get; set; } = "";

    [YamlMember(Alias = "accent_color", ApplyNamingConventions = false)]
    public string? AccentColor{ get; set; }

    [YamlMember(Alias = "background_image_path", ApplyNamingConventions = false)]
    public string? BackgroundImagePath{ get; set; }

    [YamlMember(Alias = "background_image_opacity", ApplyNamingConventions = false)]
    public double BackgroundImageOpacity{ get; set; }

    [YamlMember(Alias = "background_image_blur_radius", ApplyNamingConventions = false)]
    public double BackgroundImageBlurRadius{ get; set; }


    [YamlIgnore]
    public List<string> Override{ get; } =[];

    [YamlIgnore]
    public string CcTag{ get; set; } = "";

    [YamlIgnore]
    public bool Nocleanup{ get; } = false;

    [YamlMember(Alias = "history", ApplyNamingConventions = false)]
    public bool History{ get; set; }

    [YamlMember(Alias = "history_include_cr_artists", ApplyNamingConventions = false)]
    public bool HistoryIncludeCrArtists{ get; set; }

    [YamlMember(Alias = "history_lang", ApplyNamingConventions = false)]
    public string? HistoryLang{ get; set; }

    [YamlMember(Alias = "history_add_specials", ApplyNamingConventions = false)]
    public bool HistoryAddSpecials{ get; set; }

    [YamlMember(Alias = "history_skip_unmonitored", ApplyNamingConventions = false)]
    public bool HistorySkipUnmonitored{ get; set; }

    [YamlMember(Alias = "history_count_sonarr", ApplyNamingConventions = false)]
    public bool HistoryCountSonarr{ get; set; }

    [YamlMember(Alias = "sonarr_properties", ApplyNamingConventions = false)]
    public SonarrProperties? SonarrProperties{ get; set; }

    [YamlMember(Alias = "log_mode", ApplyNamingConventions = false)]
    public bool LogMode{ get; set; }

    [YamlMember(Alias = "download_dir_path", ApplyNamingConventions = false)]
    public string? DownloadDirPath{ get; set; }

    [YamlMember(Alias = "download_temp_dir_path", ApplyNamingConventions = false)]
    public string? DownloadTempDirPath{ get; set; }

    [YamlMember(Alias = "download_to_temp_folder", ApplyNamingConventions = false)]
    public bool DownloadToTempFolder{ get; set; }

    [YamlMember(Alias = "history_page_properties", ApplyNamingConventions = false)]
    public HistoryPageProperties? HistoryPageProperties{ get; set; }

    [YamlMember(Alias = "seasons_page_properties", ApplyNamingConventions = false)]
    public SeasonsPageProperties? SeasonsPageProperties{ get; set; }

    [YamlMember(Alias = "download_speed_limit", ApplyNamingConventions = false)]
    public int DownloadSpeedLimit{ get; set; }

    [YamlMember(Alias = "proxy_enabled", ApplyNamingConventions = false)]
    public bool ProxyEnabled{ get; set; }

    [YamlMember(Alias = "proxy_socks", ApplyNamingConventions = false)]
    public bool ProxySocks{ get; set; }

    [YamlMember(Alias = "proxy_host", ApplyNamingConventions = false)]
    public string? ProxyHost{ get; set; }

    [YamlMember(Alias = "proxy_port", ApplyNamingConventions = false)]
    public int ProxyPort{ get; set; }

    [YamlMember(Alias = "proxy_username", ApplyNamingConventions = false)]
    public string? ProxyUsername{ get; set; }

    [YamlMember(Alias = "proxy_password", ApplyNamingConventions = false)]
    public string? ProxyPassword{ get; set; }

    #endregion
    
    #region Crunchyroll Settings

    [YamlIgnore]
    public bool UseCrBetaApi{ get; set; }

    [YamlMember(Alias = "hard_sub_lang", ApplyNamingConventions = false)]
    public string Hslang{ get; set; } = "";

    [YamlIgnore]
    public int Kstream{ get; set; }

    [YamlMember(Alias = "no_video", ApplyNamingConventions = false)]
    public bool Novids{ get; set; }

    [YamlMember(Alias = "no_audio", ApplyNamingConventions = false)]
    public bool Noaudio{ get; set; }

    [YamlIgnore]
    public int StreamServer{ get; set; }

    [YamlMember(Alias = "quality_video", ApplyNamingConventions = false)]
    public string QualityVideo{ get; set; } = "";

    [YamlMember(Alias = "quality_audio", ApplyNamingConventions = false)]
    public string QualityAudio{ get; set; } = "";

    [YamlMember(Alias = "file_name", ApplyNamingConventions = false)]
    public string FileName{ get; set; } = "";

    [YamlMember(Alias = "leading_numbers", ApplyNamingConventions = false)]
    public int Numbers{ get; set; }

    [YamlMember(Alias = "download_part_size", ApplyNamingConventions = false)]
    public int Partsize{ get; set; }


    [YamlMember(Alias = "soft_subs", ApplyNamingConventions = false)]
    public List<string> DlSubs{ get; set; } =[];

    [YamlIgnore]
    public bool SkipSubs{ get; set; }

    [YamlMember(Alias = "mux_skip_subs", ApplyNamingConventions = false)]
    public bool SkipSubsMux{ get; set; }

    [YamlMember(Alias = "subs_add_scaled_border", ApplyNamingConventions = false)]
    public ScaledBorderAndShadowSelection SubsAddScaledBorder{ get; set; }

    [YamlMember(Alias = "include_signs_subs", ApplyNamingConventions = false)]
    public bool IncludeSignsSubs{ get; set; }

    [YamlMember(Alias = "mux_signs_subs_flag", ApplyNamingConventions = false)]
    public bool SignsSubsAsForced{ get; set; }

    [YamlMember(Alias = "include_cc_subs", ApplyNamingConventions = false)]
    public bool IncludeCcSubs{ get; set; }

    [YamlMember(Alias = "cc_subs_font", ApplyNamingConventions = false)]
    public string? CcSubsFont{ get; set; }

    [YamlMember(Alias = "mux_cc_subs_flag", ApplyNamingConventions = false)]
    public bool CcSubsMuxingFlag{ get; set; }

    [YamlMember(Alias = "mux_mp4", ApplyNamingConventions = false)]
    public bool Mp4{ get; set; }

    [YamlMember(Alias = "mux_video_title", ApplyNamingConventions = false)]
    public string? VideoTitle{ get; set; }

    [YamlMember(Alias = "mux_video_description", ApplyNamingConventions = false)]
    public bool IncludeVideoDescription{ get; set; }

    [YamlMember(Alias = "mux_description_lang", ApplyNamingConventions = false)]
    public string? DescriptionLang{ get; set; }

    [YamlMember(Alias = "mux_ffmpeg", ApplyNamingConventions = false)]
    public List<string> FfmpegOptions{ get; set; } =[];

    [YamlMember(Alias = "mux_mkvmerge", ApplyNamingConventions = false)]
    public List<string> MkvmergeOptions{ get; set; } =[];

    [YamlMember(Alias = "mux_default_sub", ApplyNamingConventions = false)]
    public string DefaultSub{ get; set; } = "";

    [YamlMember(Alias = "mux_default_sub_signs", ApplyNamingConventions = false)]
    public bool DefaultSubSigns{ get; set; }

    [YamlMember(Alias = "mux_default_sub_forced_display", ApplyNamingConventions = false)]
    public bool DefaultSubForcedDisplay{ get; set; }

    [YamlMember(Alias = "mux_default_dub", ApplyNamingConventions = false)]
    public string DefaultAudio{ get; set; } = "";

    [YamlMember(Alias = "dl_video_once", ApplyNamingConventions = false)]
    public bool DlVideoOnce{ get; set; }

    [YamlMember(Alias = "keep_dubs_seperate", ApplyNamingConventions = false)]
    public bool KeepDubsSeperate{ get; set; }

    [YamlMember(Alias = "mux_skip_muxing", ApplyNamingConventions = false)]
    public bool SkipMuxing{ get; set; }

    [YamlMember(Alias = "mux_sync_dubs", ApplyNamingConventions = false)]
    public bool SyncTiming{ get; set; }

    [YamlMember(Alias = "encode_enabled", ApplyNamingConventions = false)]
    public bool IsEncodeEnabled{ get; set; }

    [YamlMember(Alias = "encode_preset", ApplyNamingConventions = false)]
    public string? EncodingPresetName{ get; set; }

    [YamlMember(Alias = "chapters", ApplyNamingConventions = false)]
    public bool Chapters{ get; set; }

    [YamlMember(Alias = "dub_lang", ApplyNamingConventions = false)]
    public List<string> DubLang{ get; set; } =[];

    [YamlMember(Alias = "calendar_language", ApplyNamingConventions = false)]
    public string? SelectedCalendarLanguage{ get; set; }

    [YamlMember(Alias = "calendar_dub_filter", ApplyNamingConventions = false)]
    public string? CalendarDubFilter{ get; set; }

    [YamlMember(Alias = "calendar_custom", ApplyNamingConventions = false)]
    public bool CustomCalendar{ get; set; }

    [YamlMember(Alias = "calendar_hide_dubs", ApplyNamingConventions = false)]
    public bool CalendarHideDubs{ get; set; }

    [YamlMember(Alias = "calendar_filter_by_air_date", ApplyNamingConventions = false)]
    public bool CalendarFilterByAirDate{ get; set; }

    [YamlMember(Alias = "calendar_show_upcoming_episodes", ApplyNamingConventions = false)]
    public bool CalendarShowUpcomingEpisodes{ get; set; }

    [YamlMember(Alias = "stream_endpoint", ApplyNamingConventions = false)]
    public string? StreamEndpoint{ get; set; }

    [YamlMember(Alias = "search_fetch_featured_music", ApplyNamingConventions = false)]
    public bool SearchFetchFeaturedMusic{ get; set; }

    #endregion
}