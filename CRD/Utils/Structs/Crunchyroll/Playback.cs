using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Structs.Crunchyroll;

public class PlaybackData{
    public int Total{ get; set; }
    public  Dictionary<string, StreamDetails>? Data{ get; set; }
    public PlaybackMeta? Meta{ get; set; }
}

public class StreamDetails{
    [JsonProperty("hardsub_locale")]
    public Locale? HardsubLocale{ get; set; }

    public string? Url{ get; set; }

    [JsonProperty("hardsub_lang")]
    public string? HardsubLang{ get; set; }

    [JsonProperty("audio_lang")]
    public string? AudioLang{ get; set; }

    public string? Type{ get; set; }
}

public class PlaybackMeta{
    [JsonProperty("media_id")]
    public string? MediaId{ get; set; }

    public Subtitles? Subtitles{ get; set; }
    public List<string>? Bifs{ get; set; }
    public List<PlaybackVersion>? Versions{ get; set; }

    [JsonProperty("audio_locale")]
    public Locale? AudioLocale{ get; set; }

    [JsonProperty("closed_captions")]
    public Subtitles? ClosedCaptions{ get; set; }

    public Dictionary<string, Caption>? Captions{ get; set; }
}

public class SubtitleInfo{
    public string? Format{ get; set; }
    public Locale? Locale{ get; set; }
    public string? Url{ get; set; }
}

public class CrunchyStreams : Dictionary<string, StreamDetails>;

public class Subtitles : Dictionary<string, SubtitleInfo>;

public class PlaybackVersion{
    [JsonProperty("audio_locale")]
    public Locale AudioLocale{ get; set; } // Assuming Locale is defined elsewhere

    public string? Guid{ get; set; }

    [JsonProperty("is_premium_only")]
    public bool IsPremiumOnly{ get; set; }

    [JsonProperty("media_guid")]
    public string? MediaGuid{ get; set; }

    public bool Original{ get; set; }

    [JsonProperty("season_guid")]
    public string? SeasonGuid{ get; set; }

    public string? Variant{ get; set; }
}

public class StreamDetailsPop{
    public Locale? HardsubLocale{ get; set; }
    public string? Url{ get; set; }
    public string? HardsubLang{ get; set; }
    public string? AudioLang{ get; set; }
    public string? Type{ get; set; }
    public string? Format{ get; set; }
}