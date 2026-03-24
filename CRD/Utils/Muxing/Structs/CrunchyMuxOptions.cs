using System.Collections.Generic;
using CRD.Utils.Structs;

namespace CRD.Utils.Muxing.Structs;

public class CrunchyMuxOptions{
    public required List<string> DubLangList{ get; set; } = [];
    public required List<string> SubLangList{ get; set; } = [];
    public string Output{ get; set; }
    public bool SkipSubMux{ get; set; }
    public bool KeepAllVideos{ get; set; }
    public bool Novids{ get; set; }
    public bool Mp4{ get; set; }
    public bool Mp3{ get; set; }
    public bool MuxFonts{ get; set; }
    public bool MuxCover{ get; set; }
    public bool MuxDescription{ get; set; }
    public string ForceMuxer{ get; set; }
    public bool NoCleanup{ get; set; }
    public string VideoTitle{ get; set; }
    public List<string> FfmpegOptions{ get; set; } = [];
    public List<string> MkvmergeOptions{ get; set; } = [];
    public LanguageItem? DefaultSub{ get; set; }
    public LanguageItem? DefaultAudio{ get; set; }
    public string CcTag{ get; set; }
    public bool SyncTiming{ get; set; }

    public bool DlVideoOnce{ get; set; }

    public bool DefaultSubSigns{ get; set; }
    public bool DefaultSubForcedDisplay{ get; set; }
    public bool CcSubsMuxingFlag{ get; set; }
    public bool SignsSubsAsForced{ get; set; }
}