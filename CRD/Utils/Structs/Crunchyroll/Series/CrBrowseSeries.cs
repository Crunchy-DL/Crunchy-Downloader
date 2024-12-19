using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Media.Imaging;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class CrBrowseSeriesBase{
    public int Total{ get; set; }
    public List<CrBrowseSeries>? Data{ get; set; }
    public Meta Meta{ get; set; }
}

public class CrBrowseSeries : INotifyPropertyChanged{
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

    [JsonProperty("series_metadata")]
    public CrBrowseSeriesMetaData SeriesMetadata{ get; set; }

    public string? Id{ get; set; }

    public Images Images{ get; set; }

    [JsonProperty("promo_description")]
    public string? PromoDescription{ get; set; }

    public string? Slug{ get; set; }

    public string? Type{ get; set; }

    [JsonProperty("channel_id")]
    public string? ChannelId{ get; set; }
    
    [JsonIgnore]
    public Bitmap? ImageBitmap{ get; set; }
    
    [JsonIgnore]
    public bool IsInHistory{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public async void LoadImage(string url){
        ImageBitmap = await Helpers.LoadImage(url);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageBitmap)));
    }
    
}

public class CrBrowseSeriesMetaData{
    [JsonProperty("audio_locales")]
    public List<Locale>? AudioLocales{ get; set; }

    [JsonProperty("awards")]
    public List<object> awards{ get; set; }
    
    [JsonProperty("availability_notes")]
    public string? AvailabilityNotes{ get; set; }

    [JsonProperty("content_descriptors")]
    public List<string>? ContentDescriptors{ get; set; }

    [JsonProperty("episode_count")]
    public int EpisodeCount{ get; set; }

    [JsonProperty("extended_description")]
    public string? ExtendedDescription{ get; set; }

    [JsonProperty("extended_maturity_rating")]
    public Dictionary<object, object>?
        ExtendedMaturityRating{ get; set; }

    [JsonProperty("is_dubbed")]
    public bool IsDubbed{ get; set; }

    [JsonProperty("is_mature")]
    public bool IsMature{ get; set; }

    [JsonProperty("is_simulcast")]
    public bool IsSimulcast{ get; set; }

    [JsonProperty("is_subbed")]
    public bool IsSubbed{ get; set; }

    [JsonProperty("mature_blocked")]
    public bool MatureBlocked{ get; set; }

    [JsonProperty("maturity_ratings")]
    public List<string>? MaturityRatings{ get; set; }

    [JsonProperty("season_count")]
    public int SeasonCount{ get; set; }

    [JsonProperty("series_launch_year")]
    public int SeriesLaunchYear{ get; set; }

    [JsonProperty("subtitle_locales")]
    public List<Locale>? SubtitleLocales{ get; set; }

    [JsonProperty("tenant_categories")]
    public List<string>? TenantCategories{ get; set; }
}