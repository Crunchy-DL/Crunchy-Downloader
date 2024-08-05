using System.Collections.Generic;
using CRD.Utils.Sonarr;
using CRD.ViewModels;
using YamlDotNet.Serialization;

namespace CRD.Utils.Structs;

public class CrDownloadOptions{
    
    [YamlMember(Alias = "auto_download", ApplyNamingConventions = false)]
    public bool AutoDownload{ get; set; }

    
    [YamlMember(Alias = "remove_finished_downloads", ApplyNamingConventions = false)]
    public bool RemoveFinishedDownload{ get; set; }

    
    [YamlMember(Alias = "hard_sub_lang", ApplyNamingConventions = false)]
    public string Hslang{ get; set; }

    [YamlIgnore]
    public int Kstream{ get; set; }

    [YamlMember(Alias = "no_video", ApplyNamingConventions = false)]
    public bool Novids{ get; set; }

    [YamlMember(Alias = "no_audio", ApplyNamingConventions = false)]
    public bool Noaudio{ get; set; }

    [YamlIgnore]
    public int StreamServer{ get; set; }

    [YamlMember(Alias = "quality_video", ApplyNamingConventions = false)]
    public string QualityVideo{ get; set; }

    [YamlMember(Alias = "quality_audio", ApplyNamingConventions = false)]
    public string QualityAudio{ get; set; }

    [YamlMember(Alias = "file_name", ApplyNamingConventions = false)]
    public string FileName{ get; set; }

    [YamlMember(Alias = "leading_numbers", ApplyNamingConventions = false)]
    public int Numbers{ get; set; }

    [YamlIgnore]
    public int Partsize{ get; set; }

    [YamlIgnore]
    public int Timeout{ get; set; }

    [YamlIgnore]
    public int Waittime{ get; set; }

    [YamlIgnore]
    public int FsRetryTime{ get; set; }

    [YamlMember(Alias = "soft_subs", ApplyNamingConventions = false)]
    public List<string> DlSubs{ get; set; }

    [YamlIgnore]
    public bool SkipSubs{ get; set; }

    [YamlMember(Alias = "mux_skip_subs", ApplyNamingConventions = false)]
    public bool SkipSubsMux{ get; set; }
    
    [YamlMember(Alias = "subs_add_scaled_border", ApplyNamingConventions = false)]
    public ScaledBorderAndShadowSelection SubsAddScaledBorder{ get; set; }
    
    [YamlMember(Alias = "include_signs_subs", ApplyNamingConventions = false)]
    public bool IncludeSignsSubs{ get; set; }
    
    [YamlMember(Alias = "mux_mp4", ApplyNamingConventions = false)]
    public bool Mp4{ get; set; }

    [YamlIgnore]
    public List<string> Override{ get; set; }

    [YamlMember(Alias = "mux_video_title", ApplyNamingConventions = false)]
    public string? VideoTitle{ get; set; }
    
    [YamlMember(Alias = "mux_video_description", ApplyNamingConventions = false)]
    public bool IncludeVideoDescription{ get; set; }
    
    [YamlMember(Alias = "mux_description_lang", ApplyNamingConventions = false)]
    public string? DescriptionLang{ get; set; }

    [YamlIgnore]
    public string Force{ get; set; }

    [YamlMember(Alias = "mux_ffmpeg", ApplyNamingConventions = false)]
    public List<string> FfmpegOptions{ get; set; }

    [YamlMember(Alias = "mux_mkvmerge", ApplyNamingConventions = false)]
    public List<string> MkvmergeOptions{ get; set; }

    [YamlMember(Alias = "mux_default_sub", ApplyNamingConventions = false)]
    public string DefaultSub{ get; set; }
    
    [YamlMember(Alias = "mux_default_sub_signs", ApplyNamingConventions = false)]
    public bool DefaultSubSigns{ get; set; }
    
    [YamlMember(Alias = "mux_default_sub_forced_display", ApplyNamingConventions = false)]
    public bool DefaultSubForcedDisplay{ get; set; }

    [YamlMember(Alias = "mux_default_dub", ApplyNamingConventions = false)]
    public string DefaultAudio{ get; set; }

    [YamlIgnore]
    public string CcTag{ get; set; }

    [YamlMember(Alias = "dl_video_once", ApplyNamingConventions = false)]
    public bool DlVideoOnce{ get; set; }
    
    [YamlMember(Alias = "keep_dubs_seperate", ApplyNamingConventions = false)]
    public bool KeepDubsSeperate{ get; set; }

    [YamlIgnore]
    public bool? Skipmux{ get; set; }

    [YamlMember(Alias = "mux_sync_dubs", ApplyNamingConventions = false)]
    public bool SyncTiming{ get; set; }

    [YamlIgnore]
    public bool Nocleanup{ get; set; }

    [YamlMember(Alias = "chapters", ApplyNamingConventions = false)]
    public bool Chapters{ get; set; }

    [YamlMember(Alias = "dub_lang", ApplyNamingConventions = false)]
    public List<string> DubLang{ get; set; }

    [YamlMember(Alias = "simultaneous_downloads", ApplyNamingConventions = false)]
    public int SimultaneousDownloads{ get; set; }

    [YamlMember(Alias = "theme", ApplyNamingConventions = false)]
    public string Theme{ get; set; }

    [YamlMember(Alias = "accent_color", ApplyNamingConventions = false)]
    public string? AccentColor{ get; set; }

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

    [YamlMember(Alias = "history", ApplyNamingConventions = false)]
    public bool History{ get; set; }
    
    [YamlMember(Alias = "history_lang", ApplyNamingConventions = false)]
    public string? HistoryLang{ get; set; }
    
    [YamlMember(Alias = "history_add_specials", ApplyNamingConventions = false)]
    public bool HistoryAddSpecials{ get; set; }
    
    [YamlMember(Alias = "history_count_sonarr", ApplyNamingConventions = false)]
    public bool HistoryCountSonarr{ get; set; }
    
    [YamlMember(Alias = "sonarr_properties", ApplyNamingConventions = false)]
    public SonarrProperties? SonarrProperties{ get; set; }
    
    [YamlMember(Alias = "log_mode", ApplyNamingConventions = false)]
    public bool LogMode{ get; set; }
 
    [YamlMember(Alias = "stream_endpoint", ApplyNamingConventions = false)]
    public string? StreamEndpoint{ get; set; }
    
    [YamlMember(Alias = "download_dir_path", ApplyNamingConventions = false)]
    public string? DownloadDirPath{ get; set; }
    
    [YamlMember(Alias = "history_page_properties", ApplyNamingConventions = false)]
    public HistoryPageProperties? HistoryPageProperties{ get; set; }
    
    [YamlMember(Alias = "download_speed_limit", ApplyNamingConventions = false)]
    public int DownloadSpeedLimit{ get; set; }
    
}