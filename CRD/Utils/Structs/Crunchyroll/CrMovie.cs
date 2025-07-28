using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class CrunchyMovieList{
    public int Total{ get; set; }
    public List<CrunchyMovie>? Data{ get; set; }
    public Meta Meta{ get; set; }
}

public class CrunchyMovie{
    [JsonProperty("channel_id")]
    public string? ChannelId{ get; set; }

    [JsonProperty("content_descriptors")]
    public List<string> ContentDescriptors{ get; set; }

    [JsonProperty("mature_blocked")]
    public bool MatureBlocked{ get; set; }

    [JsonProperty("is_premium_only")]
    public bool IsPremiumOnly{ get; set; }

    [JsonProperty("is_mature")]
    public bool IsMature{ get; set; }

    [JsonProperty("free_available_date")]
    public DateTime FreeAvailableDate{ get; set; }

    [JsonProperty("premium_available_date")]
    public DateTime PremiumAvailableDate{ get; set; }

    [JsonProperty("availability_starts")]
    public DateTime AvailabilityStarts{ get; set; }

    [JsonProperty("availability_ends")]
    public DateTime AvailabilityEnds{ get; set; }

    [JsonProperty("maturity_ratings")]
    public List<string> MaturityRatings{ get; set; }

    [JsonProperty("movie_listing_title")]
    public string? MovieListingTitle{ get; set; }

    public string Id{ get; set; }

    public string Title{ get; set; }

    [JsonProperty("duration_ms")]
    public int DurationMs{ get; set; }

    [JsonProperty("listing_id")]
    public string ListingId{ get; set; }

    [JsonProperty("available_date")]
    public DateTime AvailableDate{ get; set; }

    [JsonProperty("is_subbed")]
    public bool IsSubbed{ get; set; }

    public string Slug{ get; set; }

    [JsonProperty("available_offline")]
    public bool AvailableOffline{ get; set; }

    [JsonProperty("availability_notes")]
    public string AvailabilityNotes{ get; set; }

    [JsonProperty("closed_captions_available")]
    public bool ClosedCaptionsAvailable{ get; set; }

    [JsonProperty("audio_locale")]
    public string AudioLocale{ get; set; }

    [JsonProperty("is_dubbed")]
    public bool IsDubbed{ get; set; }

    [JsonProperty("streams_link")]
    public string? StreamsLink{ get; set; }

    [JsonProperty("slug_title")]
    public string SlugTitle{ get; set; }

    public string Description{ get; set; }

    public Images Images{ get; set; } = new();

    [JsonProperty("media_type")]
    public string? MediaType{ get; set; }

    [JsonProperty("extended_maturity_rating")]
    public Dictionary<string, object> ExtendedMaturityRating{ get; set; }

    [JsonProperty("premium_date")]
    public DateTime PremiumDate{ get; set; }
    
    [JsonProperty("type")]
    public string type{ get; set; }
    
}