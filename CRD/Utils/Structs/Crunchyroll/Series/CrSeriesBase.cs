using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class CrSeriesBase{
    public int Total{ get; set; }
    public SeriesBaseItem[]? Data{ get; set; }
    public Meta Meta{ get; set; }
}

public class SeriesBaseItem{
    [JsonProperty("extended_maturity_rating")]
    public Dictionary<object, object>
        ExtendedMaturityRating{ get; set; }

    [JsonProperty("extended_description")]
    public string ExtendedDescription{ get; set; }

    [JsonProperty("episode_count")]
    public int EpisodeCount{ get; set; }

    [JsonProperty("is_mature")]
    public bool IsMature{ get; set; }


    public Images Images{ get; set; }


    [JsonProperty("season_count")]
    public int SeasonCount{ get; set; }

    [JsonProperty("content_descriptors")]
    public List<string> ContentDescriptors{ get; set; }


    public string Id{ get; set; }


    [JsonProperty("media_count")]
    public int MediaCount{ get; set; }


    [JsonProperty("is_simulcast")]
    public bool IsSimulcast{ get; set; }

    [JsonProperty("seo_description")]
    public string SeoDescription{ get; set; }

    [JsonProperty("availability_notes")]
    public string AvailabilityNotes{ get; set; }

    [JsonProperty("season_tags")]
    public List<string> SeasonTags{ get; set; }

    [JsonProperty("maturity_ratings")]
    public List<string> MaturityRatings{ get; set; }

    [JsonProperty("mature_blocked")]
    public bool MatureBlocked{ get; set; }

    [JsonProperty("is_dubbed")]
    public bool IsDubbed{ get; set; }

    [JsonProperty("series_launch_year")]
    public int SeriesLaunchYear{ get; set; }

    public string Slug{ get; set; }

    [JsonProperty("content_provider")]
    public string ContentProvider{ get; set; }

    [JsonProperty("subtitle_locales")]
    public List<string> SubtitleLocales{ get; set; }

    public string Title{ get; set; }

    [JsonProperty("is_subbed")]
    public bool IsSubbed{ get; set; }

    [JsonProperty("seo_title")]
    public string SeoTitle{ get; set; }

    [JsonProperty("channel_id")]
    public string ChannelId{ get; set; }

    [JsonProperty("slug_title")]
    public string SlugTitle{ get; set; }

    public string Description{ get; set; }

    public List<string> Keywords{ get; set; }

    [JsonProperty("audio_locales")]
    public List<string> AudioLocales{ get; set; }
}