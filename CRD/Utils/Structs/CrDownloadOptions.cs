using System.Collections.Generic;
using CRD.Utils.Sonarr;
using YamlDotNet.Serialization;

namespace CRD.Utils.Structs;

public class CrDownloadOptions{
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
    
    [YamlMember(Alias = "mux_mp4", ApplyNamingConventions = false)]
    public bool Mp4{ get; set; }

    [YamlIgnore]
    public List<string> Override{ get; set; }

    [YamlIgnore]
    public string VideoTitle{ get; set; }

    [YamlIgnore]
    public string Force{ get; set; }

    [YamlMember(Alias = "mux_ffmpeg", ApplyNamingConventions = false)]
    public List<string> FfmpegOptions{ get; set; }

    [YamlMember(Alias = "mux_mkvmerge", ApplyNamingConventions = false)]
    public List<string> MkvmergeOptions{ get; set; }

    [YamlMember(Alias = "mux_default_sub", ApplyNamingConventions = false)]
    public string DefaultSub{ get; set; }

    [YamlMember(Alias = "mux_default_dub", ApplyNamingConventions = false)]
    public string DefaultAudio{ get; set; }

    [YamlIgnore]
    public string CcTag{ get; set; }

    [YamlMember(Alias = "dl_video_once", ApplyNamingConventions = false)]
    public bool DlVideoOnce{ get; set; }

    [YamlIgnore]
    public bool? Skipmux{ get; set; }

    [YamlIgnore]
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

    [YamlIgnore]
    public string? SelectedCalendarLanguage{ get; set; }

    [YamlMember(Alias = "history", ApplyNamingConventions = false)]
    public bool History{ get; set; }
    
    [YamlMember(Alias = "sonarr_properties", ApplyNamingConventions = false)]
    public SonarrProperties? SonarrProperties{ get; set; }
    
    [YamlMember(Alias = "log_mode", ApplyNamingConventions = false)]
    public bool LogMode{ get; set; }
 
    [YamlMember(Alias = "stream_endpoint", ApplyNamingConventions = false)]
    public string? StreamEndpoint{ get; set; }
    
}