using CRD.Utils.Structs;

namespace CRD.Utils.Muxing.Structs;

public class MergerInput{
    public string Path{ get; set; }
    public LanguageItem Language{ get; set; }
    public int? Duration{ get; set; }
    public int? Delay{ get; set; }
    public bool IsAudioRoleDescription{ get; set; }
    public int? Bitrate{ get; set; }
}