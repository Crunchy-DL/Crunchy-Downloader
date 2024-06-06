using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class CrunchyStreamData{
    public string? AssetId{ get; set; }
    public Locale? AudioLocale{ get; set; }
    public string? Bifs{ get; set; }
    public string? BurnedInLocale{ get; set; }
    public Dictionary<string, Caption>? Captions{ get; set; }
    public Dictionary<string, HardSub>? HardSubs{ get; set; }
    public string? PlaybackType{ get; set; }
    public Session? Session{ get; set; }
    public Dictionary<string, Subtitle>? Subtitles{ get; set; }
    public string? Token{ get; set; }
    public string? Url{ get; set; }
    public List<PlaybackVersion>? Versions{ get; set; } 
}

public class Caption{
    public string? Format{ get; set; }
    public string? Language{ get; set; }
    public string? Url{ get; set; }
}

public class HardSub{
    public string? Hlang{ get; set; }
    public string? Url{ get; set; }
    public string? Quality{ get; set; }
}

public class Session{
    public int? RenewSeconds{ get; set; }
    public int? NoNetworkRetryIntervalSeconds{ get; set; }
    public int? NoNetworkTimeoutSeconds{ get; set; }
    public int? MaximumPauseSeconds{ get; set; }
    public int? EndOfVideoUnloadSeconds{ get; set; }
    public int? SessionExpirationSeconds{ get; set; }
    public bool? UsesStreamLimits{ get; set; }
}

public class Subtitle{
    public string? Format{ get; set; }
    [JsonProperty("language")]
    public Locale? Locale{ get; set; }
    public string? Url{ get; set; }
}

