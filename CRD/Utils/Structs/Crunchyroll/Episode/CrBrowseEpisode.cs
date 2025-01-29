using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Media.Imaging;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class CrBrowseEpisodeBase{
    public int Total{ get; set; }
    public List<CrBrowseEpisode>? Data{ get; set; }
    public Meta Meta{ get; set; }
}

public class CrBrowseEpisode : INotifyPropertyChanged{
    [JsonProperty("external_id")]
    public string? ExternalId{ get; set; }

    [JsonProperty("last_public")]
    public DateTime LastPublic{ get; set; }

    public string? Description{ get; set; }

    public bool New{ get; set; }

    [JsonProperty("linked_resource_key")]
    public string? LinkedResourceKey{ get; set; }

    [JsonProperty("slug_title")]
    public string? SlugTitle{ get; set; }

    public string? Title{ get; set; }

    [JsonProperty("promo_title")]
    public string? PromoTitle{ get; set; }

    [JsonProperty("episode_metadata")]
    public CrBrowseEpisodeMetaData EpisodeMetadata{ get; set; }

    public string? Id{ get; set; }

    public Images Images{ get; set; }

    [JsonProperty("promo_description")]
    public string? PromoDescription{ get; set; }

    public string? Slug{ get; set; }

    public string? Type{ get; set; }

    [JsonProperty("channel_id")]
    public string? ChannelId{ get; set; }
    
    [JsonProperty("streams_link")]
    public string? StreamsLink{ get; set; }
    
    [JsonIgnore]
    public Bitmap? ImageBitmap{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public async void LoadImage(string url){
        ImageBitmap = await Helpers.LoadImage(url);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageBitmap)));
    }
    
}

public class CrBrowseEpisodeMetaData{
    [JsonProperty("audio_locale")]
    public Locale? AudioLocale{ get; set; }

    [JsonProperty("content_descriptors")]
    public List<string>? ContentDescriptors{ get; set; }
    
    [JsonProperty("availability_notes")]
    public string? AvailabilityNotes{ get; set; }
    
    public string? Episode{ get; set; }
    
    [JsonProperty("episode_air_date")]
    public DateTime EpisodeAirDate{ get; set; }
    
    [JsonProperty("episode_number")]
    public int EpisodeCount{ get; set; }
    
    [JsonProperty("duration_ms")]
    public int DurationMs{ get; set; }
    
    [JsonProperty("extended_maturity_rating")]
    public Dictionary<object, object>?
        ExtendedMaturityRating{ get; set; }
    
    [JsonProperty("is_dubbed")]
    public bool IsDubbed{ get; set; }
    
    [JsonProperty("is_mature")]
    public bool IsMature{ get; set; }

    [JsonProperty("is_subbed")]
    public bool IsSubbed{ get; set; }

    [JsonProperty("mature_blocked")]
    public bool MatureBlocked{ get; set; }

    [JsonProperty("is_premium_only")]
    public bool IsPremiumOnly{ get; set; }
    
    [JsonProperty("is_clip")]
    public bool IsClip{ get; set; }
   
    [JsonProperty("maturity_ratings")]
    public List<string>? MaturityRatings{ get; set; }
  
    [JsonProperty("season_number")]
    public double SeasonNumber{ get; set; }
    
    [JsonProperty("season_sequence_number")]
    public double SeasonSequenceNumber{ get; set; }

    [JsonProperty("sequence_number")]
    public double SequenceNumber{ get; set; }
    
    [JsonProperty("upload_date")]
    public DateTime UploadDate{ get; set; }

    [JsonProperty("subtitle_locales")]
    public List<Locale>? SubtitleLocales{ get; set; }

    [JsonProperty("premium_available_date")]
    public DateTime PremiumAvailableDate{ get; set; }


    [JsonProperty("availability_ends")]
    public DateTime AvailabilityEnds{ get; set; }
    
    
    [JsonProperty("availability_starts")]
    public DateTime AvailabilityStarts{ get; set; }
    
    
    [JsonProperty("free_available_date")]
    public DateTime FreeAvailableDate{ get; set; }

    [JsonProperty("identifier")]
    public string? Identifier{ get; set; }

    [JsonProperty("season_id")]
    public string? SeasonId{ get; set; }
    
    [JsonProperty("series_id")]
    public string? SeriesId{ get; set; }
  
    [JsonProperty("season_display_number")]
    public string? SeasonDisplayNumber{ get; set; }

    [JsonProperty("eligible_region")]
    public string? EligibleRegion{ get; set; }
    
    [JsonProperty("available_date")]
    public DateTime AvailableDate{ get; set; }
    
    [JsonProperty("premium_date")]
    public DateTime PremiumDate{ get; set; }
    
    [JsonProperty("available_offline")]
    public bool AvailableOffline{ get; set; }
    
    [JsonProperty("closed_captions_available")]
    public bool ClosedCaptionsAvailable{ get; set; }
    
    [JsonProperty("season_slug_title")]
    public string? SeasonSlugTitle{ get; set; }
    
    [JsonProperty("season_title")]
    public string? SeasonTitle{ get; set; }
    
    [JsonProperty("series_slug_title")]
    public string? SeriesSlugTitle{ get; set; }
    
    [JsonProperty("series_title")]
    public string? SeriesTitle{ get; set; }
    
    [JsonProperty("versions")]
    public List<CrBrowseEpisodeVersion>? versions{ get; set; }
 
}

public class CrBrowseEpisodeVersion{
    [JsonProperty("audio_locale")]
    public Locale? AudioLocale{ get; set; }

    public string? Guid{ get; set; }
    public bool? Original{ get; set; }
    public string? Variant{ get; set; }
    
    [JsonProperty("season_guid")]
    public string? SeasonGuid{ get; set; }
    
    [JsonProperty("media_guid")]
    public string? MediaGuid{ get; set; }
    
    [JsonProperty("is_premium_only")]
    public bool? IsPremiumOnly{ get; set; }
    
}