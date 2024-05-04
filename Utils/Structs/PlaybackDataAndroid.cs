using System.Collections.Generic;

namespace CRD.Utils.Structs;

public class PlaybackDataAndroid{
    public string __class__{ get; set; }
    public string __href__{ get; set; }
    public string __resource_key__{ get; set; }
    public Links __links__{ get; set; }
    public Dictionary<object, object> __actions__{ get; set; }
    public string media_id{ get; set; }
    public Locale audio_locale{ get; set; }
    public Subtitles subtitles{ get; set; }
    public Subtitles closed_captions{ get; set; }
    public List<Dictionary<string, Dictionary<string, StreamDetails>>> streams{ get; set; }
    public List<string> bifs{ get; set; }
    public List<PlaybackVersion> versions{ get; set; }
    public Dictionary<string, object> captions{ get; set; }
}