using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public struct CrunchyEpisodeList{
    public int Total{ get; set; }
    public List<CrunchyEpisode>? Data{ get; set; }
    public Meta Meta{ get; set; }
}

public struct CrunchyEpisode{
    [JsonProperty("next_episode_id")]
    public string NextEpisodeId{ get; set; }

    [JsonProperty("series_id")]
    public string SeriesId{ get; set; }

    [JsonProperty("season_number")]
    public int SeasonNumber{ get; set; }

    [JsonProperty("next_episode_title")]
    public string NextEpisodeTitle{ get; set; }

    [JsonProperty("availability_notes")]
    public string AvailabilityNotes{ get; set; }

    [JsonProperty("duration_ms")]
    public int DurationMs{ get; set; }

    [JsonProperty("series_slug_title")]
    public string SeriesSlugTitle{ get; set; }

    [JsonProperty("series_title")]
    public string SeriesTitle{ get; set; }

    [JsonProperty("is_dubbed")]
    public bool IsDubbed{ get; set; }

    public List<EpisodeVersion>? Versions{ get; set; }
    public string Identifier{ get; set; }

    [JsonProperty("sequence_number")]
    public float SequenceNumber{ get; set; }

    [JsonProperty("eligible_region")]
    public string EligibleRegion{ get; set; }

    [JsonProperty("availability_starts")]
    public DateTime? AvailabilityStarts{ get; set; }

    public Images? Images{ get; set; }

    [JsonProperty("season_id")]
    public string SeasonId{ get; set; }

    [JsonProperty("seo_title")]
    public string SeoTitle{ get; set; }

    [JsonProperty("is_premium_only")]
    public bool IsPremiumOnly{ get; set; }

    [JsonProperty("extended_maturity_rating")]
    public Dictionary<string, object> ExtendedMaturityRating{ get; set; }

    public string Title{ get; set; }

    [JsonProperty("production_episode_id")]
    public string ProductionEpisodeId{ get; set; }

    [JsonProperty("premium_available_date")]
    public DateTime? PremiumAvailableDate{ get; set; }

    [JsonProperty("season_title")]
    public string SeasonTitle{ get; set; }

    [JsonProperty("seo_description")]
    public string SeoDescription{ get; set; }

    [JsonProperty("audio_locale")]
    public string AudioLocale{ get; set; }

    public string Id{ get; set; }

    [JsonProperty("media_type")]
    public MediaType? MediaType{ get; set; }

    [JsonProperty("availability_ends")]
    public DateTime? AvailabilityEnds{ get; set; }

    [JsonProperty("free_available_date")]
    public DateTime? FreeAvailableDate{ get; set; }

    public string Playback{ get; set; }

    [JsonProperty("channel_id")]
    public ChannelId? ChannelId{ get; set; }

    public string? Episode{ get; set; }

    [JsonProperty("is_mature")]
    public bool IsMature{ get; set; }

    [JsonProperty("listing_id")]
    public string ListingId{ get; set; }

    [JsonProperty("episode_air_date")]
    public DateTime? EpisodeAirDate{ get; set; }

    public string Slug{ get; set; }

    [JsonProperty("available_date")]
    public DateTime? AvailableDate{ get; set; }

    [JsonProperty("subtitle_locales")]
    public List<string> SubtitleLocales{ get; set; }

    [JsonProperty("slug_title")]
    public string SlugTitle{ get; set; }

    [JsonProperty("available_offline")]
    public bool AvailableOffline{ get; set; }

    public string Description{ get; set; }

    [JsonProperty("is_subbed")]
    public bool IsSubbed{ get; set; }

    [JsonProperty("premium_date")]
    public DateTime? PremiumDate{ get; set; }

    [JsonProperty("upload_date")]
    public DateTime? UploadDate{ get; set; }

    [JsonProperty("season_slug_title")]
    public string SeasonSlugTitle{ get; set; }

    [JsonProperty("closed_captions_available")]
    public bool ClosedCaptionsAvailable{ get; set; }

    [JsonProperty("episode_number")]
    public int? EpisodeNumber{ get; set; }

    [JsonProperty("season_tags")]
    public List<object> SeasonTags{ get; set; }

    [JsonProperty("maturity_ratings")]
    public List<string> MaturityRatings{ get; set; }

    [JsonProperty("streams_link")]
    public string? StreamsLink{ get; set; }

    [JsonProperty("mature_blocked")]
    public bool? MatureBlocked{ get; set; }

    [JsonProperty("is_clip")]
    public bool IsClip{ get; set; }

    [JsonProperty("hd_flag")]
    public bool HdFlag{ get; set; }

    [JsonProperty("hide_season_title")]
    public bool? HideSeasonTitle{ get; set; }

    [JsonProperty("hide_season_number")]
    public bool? HideSeasonNumber{ get; set; }

    public bool? IsSelected{ get; set; }

    [JsonProperty("seq_id")]
    public string SeqId{ get; set; }

    [JsonProperty("__links__")]
    public Links? Links{ get; set; }
}

public struct Images{
    [JsonProperty("poster_tall")]
    public List<List<Image>>? PosterTall{ get; set; }

    [JsonProperty("poster_wide")]
    public List<List<Image>>? PosterWide{ get; set; }

    [JsonProperty("promo_image")]
    public List<List<Image>>? PromoImage{ get; set; }

    public List<List<Image>> Thumbnail{ get; set; }
}

public struct Image{
    public int Height{ get; set; }
    public string Source{ get; set; }
    public ImageType Type{ get; set; }
    public int Width{ get; set; }
}

public struct EpisodeVersion{
    [JsonProperty("audio_locale")]
    public string AudioLocale{ get; set; }

    public string Guid{ get; set; }

    [JsonProperty("is_premium_only")]
    public bool IsPremiumOnly{ get; set; }

    [JsonProperty("media_guid")]
    public string? MediaGuid{ get; set; }

    public bool Original{ get; set; }

    [JsonProperty("season_guid")]
    public string SeasonGuid{ get; set; }

    public string Variant{ get; set; }
}

public struct Link{
    public string Href{ get; set; }
}

public struct Links(){
    public Dictionary<string, Link> LinkMappings{ get; set; } = new(){
        { "episode/channel", default },
        { "episode/next_episode", default },
        { "episode/season", default },
        { "episode/series", default },
        { "streams", default }
    };
}

public class CrunchyEpMeta{
    public List<CrunchyEpMetaData>? Data{ get; set; }

    public string? SeriesTitle{ get; set; }
    public string? SeasonTitle{ get; set; }
    public string? EpisodeNumber{ get; set; }
    public string? EpisodeTitle{ get; set; }
    public string? Description{ get; set; }
    public string? SeasonId{ get; set; }
    public string? Season{ get; set; }
    public string? ShowId{ get; set; }
    public string? AbsolutEpisodeNumberE{ get; set; }
    public string? Image{ get; set; }
    public bool Paused{ get; set; }
    public DownloadProgress? DownloadProgress{ get; set; }

    public List<string>? SelectedDubs{ get; set; }

    public List<string>? AvailableSubs{ get; set; }
    
    public string? DownloadPath{ get; set; }
    
}

public class DownloadProgress{
    public bool IsDownloading = false;
    public bool Done = false;
    public bool Error = false;
    public string Doing = string.Empty;

    public int Percent{ get; set; }
    public double Time{ get; set; }
    public double DownloadSpeed{ get; set; }
}

public struct CrunchyEpMetaData{
    public string MediaId{ get; set; }
    public LanguageItem? Lang{ get; set; }
    public string? Playback{ get; set; }
    public List<EpisodeVersion>? Versions{ get; set; }
    public bool IsSubbed{ get; set; }
    public bool IsDubbed{ get; set; }
    
}

public struct CrunchyRollEpisodeData{
    public string Key{ get; set; }
    public EpisodeAndLanguage EpisodeAndLanguages{ get; set; }
}