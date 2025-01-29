using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Structs.Crunchyroll.Music;


public class CrunchyArtistList{
    public int Total{ get; set; }
    public List<CrArtist> Data{ get; set; } =[];
    public Meta? Meta{ get; set; }
}

public class CrArtist{
    [JsonProperty("description")]
    public string? Description{ get; set; }
    
    [JsonProperty("name")]
    public string? Name{ get; set; }
    
    [JsonProperty("slug")]
    public string? Slug{ get; set; }
    
    [JsonProperty("type")]
    public string? Type{ get; set; }
    
    [JsonProperty("id")]
    public string? Id{ get; set; }
    
    [JsonProperty("publishDate")]
    public DateTime? PublishDate{ get; set; }
    
    public MusicImages Images{ get; set; } = new();
    
}