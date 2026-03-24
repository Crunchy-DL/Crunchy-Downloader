using CRD.Utils.Structs;

namespace CRD.Utils.Muxing.Structs;

public class SubtitleInput{
    public LanguageItem Language{ get; set; }
    public string File{ get; set; }
    public bool ClosedCaption{ get; set; }
    public bool Signs{ get; set; }
    public int? Delay{ get; set; }

    public DownloadedMedia? RelatedVideoDownloadMedia;
}