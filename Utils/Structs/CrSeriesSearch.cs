using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class CrSeriesSearch{
    public int Total{ get; set; }
    public SeriesSearchItem[]? Data{ get; set; }
    public Meta Meta{ get; set; }
}

public struct SeriesSearchItem{
    public string Description{ get; set; }
    [JsonProperty("seo_description")] public string SeoDescription{ get; set; }
    [JsonProperty("number_of_episodes")] public int NumberOfEpisodes{ get; set; }
    [JsonProperty("is_dubbed")] public bool IsDubbed{ get; set; }
    public string Identifier{ get; set; }
    [JsonProperty("channel_id")] public string ChannelId{ get; set; }
    [JsonProperty("slug_title")] public string SlugTitle{ get; set; }

    [JsonProperty("season_sequence_number")]
    public int SeasonSequenceNumber{ get; set; }

    [JsonProperty("season_tags")] public List<string> SeasonTags{ get; set; }

    [JsonProperty("extended_maturity_rating")]
    public Dictionary<object, object>
        ExtendedMaturityRating{ get; set; }

    [JsonProperty("is_mature")] public bool IsMature{ get; set; }
    [JsonProperty("audio_locale")] public string AudioLocale{ get; set; }
    [JsonProperty("season_number")] public int SeasonNumber{ get; set; }
    public Dictionary<object, object> Images{ get; set; }
    [JsonProperty("mature_blocked")] public bool MatureBlocked{ get; set; }
    public List<Version> Versions{ get; set; }
    public string Title{ get; set; }
    [JsonProperty("is_subbed")] public bool IsSubbed{ get; set; }
    public string Id{ get; set; }
    [JsonProperty("audio_locales")] public List<string> AudioLocales{ get; set; }
    [JsonProperty("subtitle_locales")] public List<string> SubtitleLocales{ get; set; }
    [JsonProperty("availability_notes")] public string AvailabilityNotes{ get; set; }
    [JsonProperty("series_id")] public string SeriesId{ get; set; }

    [JsonProperty("season_display_number")]
    public string SeasonDisplayNumber{ get; set; }

    [JsonProperty("is_complete")] public bool IsComplete{ get; set; }
    public List<string> Keywords{ get; set; }
    [JsonProperty("maturity_ratings")] public List<string> MaturityRatings{ get; set; }
    [JsonProperty("is_simulcast")] public bool IsSimulcast{ get; set; }
    [JsonProperty("seo_title")] public string SeoTitle{ get; set; }
}

public struct Version{
    [JsonProperty("audio_locale")] public string? AudioLocale{ get; set; }
    public string? Guid{ get; set; }
    public bool? Original{ get; set; }
    public string? Variant{ get; set; }
}