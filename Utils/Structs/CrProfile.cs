using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class CrProfile{
    public string? Avatar{ get; set; }
    public string? Email{ get; set; }
    public string? Username{ get; set; }
    
    [JsonProperty("preferred_content_audio_language")]
    public string? PreferredContentAudioLanguage{ get; set; }
    
    [JsonProperty("preferred_content_subtitle_language")]
    public string? PreferredContentSubtitleLanguage{ get; set; }
}