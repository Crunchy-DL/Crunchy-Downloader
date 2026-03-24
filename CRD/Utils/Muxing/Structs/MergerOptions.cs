using System.Collections.Generic;
using CRD.Utils.Structs;

namespace CRD.Utils.Muxing.Structs;

public class MergerOptions{
    public List<string> DubLangList{ get; set; } = [];
    public List<string> SubLangList{ get; set; } = [];
    public List<MergerInput> OnlyVid{ get; set; } = [];
    public List<MergerInput> OnlyAudio{ get; set; } = [];
    public List<SubtitleInput> Subtitles{ get; set; } = [];
    public List<MergerInput> Chapters{ get; set; } = [];
    public string CcTag{ get; set; }
    public string Output{ get; set; }
    public string VideoTitle{ get; set; }
    public bool KeepAllVideos{ get; set; }
    public List<ParsedFont> Fonts{ get; set; } = [];
    public bool SkipSubMux{ get; set; }
    public MuxOptions Options{ get; set; }
    public Defaults Defaults{ get; set; }
    public bool mp3{ get; set; }
    public bool DefaultSubSigns{ get; set; }
    public bool DefaultSubForcedDisplay{ get; set; }
    public bool CcSubsMuxingFlag{ get; set; }
    public bool SignsSubsAsForced{ get; set; }
    public List<MergerInput> Description{ get; set; } = [];
    public List<MergerInput> Cover{ get; set; } = [];
    public List<MergerInput> VideoAndAudio{ get; set; } = [];
}

public class Defaults{
    public LanguageItem? Audio{ get; set; }
    public LanguageItem? Sub{ get; set; }
}

public class MuxOptions{
    public List<string>? Ffmpeg{ get; set; }
    public List<string>? Mkvmerge{ get; set; }
}