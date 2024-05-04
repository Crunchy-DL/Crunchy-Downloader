using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace CRD.Utils.Structs;

public class CrDownloadOptions{
    [YamlMember(Alias = "hard_sub_lang", ApplyNamingConventions = false)]
    public string Hslang{ get; set; } //locale string none or locale

    [YamlIgnore]
    public int Kstream{ get; set; }

    [YamlMember(Alias = "no_video", ApplyNamingConventions = false)]
    public bool Novids{ get; set; } //dont download videos

    [YamlMember(Alias = "no_audio", ApplyNamingConventions = false)]
    public bool Noaudio{ get; set; } //dont download audio

    [YamlIgnore]
    public int X{ get; set; } // selected server

    [YamlMember(Alias = "quality_video", ApplyNamingConventions = false)]
    public string QualityVideo{ get; set; } //quality 0 is best

    [YamlMember(Alias = "quality_audio", ApplyNamingConventions = false)]
    public string QualityAudio{ get; set; } //quality 0 is best

    [YamlMember(Alias = "file_name", ApplyNamingConventions = false)]
    public string FileName{ get; set; } // 

    [YamlMember(Alias = "leading_numbers", ApplyNamingConventions = false)]
    public int Numbers{ get; set; } //leading 0 probably

    [YamlIgnore]
    public int Partsize{ get; set; } // download parts at same time?

    [YamlIgnore]
    public int Timeout{ get; set; }

    [YamlIgnore]
    public int Waittime{ get; set; }

    [YamlIgnore]
    public int FsRetryTime{ get; set; }

    [YamlMember(Alias = "soft_subs", ApplyNamingConventions = false)]
    public List<string> DlSubs{ get; set; } //all or local for subs to download

    [YamlIgnore]
    public bool SkipSubs{ get; set; } // don't download subs

    [YamlIgnore]
    public bool NoSubs{ get; set; } // don't download subs

    [YamlMember(Alias = "mux_mp4", ApplyNamingConventions = false)]
    public bool Mp4{ get; set; } // mp4 output else mkv

    [YamlIgnore]
    public List<string> Override{ get; set; }

    [YamlIgnore]
    public string VideoTitle{ get; set; } // ???

    [YamlIgnore]
    public string Force{ get; set; } // always Y

    [YamlMember(Alias = "mux_ffmpeg", ApplyNamingConventions = false)]
    public List<string> FfmpegOptions{ get; set; } //additional ffmpeg options

    [YamlMember(Alias = "mux_mkvmerge", ApplyNamingConventions = false)]
    public List<string> MkvmergeOptions{ get; set; } //additional mkvmerge

    [YamlIgnore]
    public LanguageItem DefaultSub{ get; set; } //default sub

    [YamlIgnore]
    public LanguageItem DefaultAudio{ get; set; } //default audio

    [YamlIgnore]
    public string CcTag{ get; set; } //cc tag ??

    [YamlIgnore]
    public bool DlVideoOnce{ get; set; } // don't download same video multiple times

    [YamlIgnore]
    public bool? Skipmux{ get; set; } //mux in the end or not

    [YamlIgnore]
    public bool SyncTiming{ get; set; } // sync timing in muxing

    [YamlIgnore]
    public bool Nocleanup{ get; set; } // cleanup files after muxing

    [YamlMember(Alias = "chapters", ApplyNamingConventions = false)]
    public bool Chapters{ get; set; } // download chaperts

    [YamlIgnore]
    public string? FontName{ get; set; } //font sutff

    [YamlIgnore]
    public bool OriginalFontSize{ get; set; } //font sutff

    [YamlIgnore]
    public int FontSize{ get; set; } //font sutff

    [YamlMember(Alias = "dub_lang", ApplyNamingConventions = false)]
    public List<string> DubLang{ get; set; } //dub lang download 

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
}