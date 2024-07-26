using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public struct CrunchyChapters{
    public List<CrunchyChapter> Chapters  { get; set; }
    public DateTime? lastUpdate { get; set; }
    public string? mediaId { get; set; }
}

public struct CrunchyChapter{
    public string approverId { get; set; }
    public string distributionNumber { get; set; }
    public double? end { get; set; }
    public double? start { get; set; }
    public string title { get; set; }
    public string seriesId { get; set; }
    [JsonProperty("new")]
    public string? New { get; set; }
    public string type { get; set; }
}

public struct CrunchyOldChapter{
    public string media_id { get; set; }
    public double startTime { get; set; }
    public double endTime { get; set; }
    public double duration { get; set; }
    public string comparedWith { get; set; }
    public string ordering { get; set; }
    public DateTime last_updated { get; set; }
}