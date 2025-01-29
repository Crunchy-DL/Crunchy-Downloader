using System;
using System.Collections.Generic;
using CRD.Utils.Structs.History;
using Newtonsoft.Json;

namespace CRD.Utils.Structs.Crunchyroll.Music;

public class CrunchyMusicVideoList{
    public int Total{ get; set; }
    public List<CrunchyMusicVideo> Data{ get; set; } =[];
    public Meta? Meta{ get; set; }
}

public class CrunchyMusicVideo : IHistorySource{
    [JsonProperty("copyright")]
    public string? Copyright{ get; set; }

    [JsonProperty("hash")]
    public string? Hash{ get; set; }

    [JsonProperty("availability")]
    public MusicVideoAvailability? Availability{ get; set; }

    [JsonProperty("isMature")]
    public bool IsMature{ get; set; }

    [JsonProperty("maturityRatings")]
    public object? MaturityRatings{ get; set; }

    [JsonProperty("title")]
    public string? Title{ get; set; }

    [JsonProperty("artists")]
    public object? Artists{ get; set; }

    [JsonProperty("displayArtistNameRequired")]
    public bool DisplayArtistNameRequired{ get; set; }

    [JsonProperty("streams_link")]
    public string? StreamsLink{ get; set; }

    [JsonProperty("matureBlocked")]
    public bool MatureBlocked{ get; set; }

    [JsonProperty("originalRelease")]
    public DateTime OriginalRelease{ get; set; }

    [JsonProperty("sequenceNumber")]
    public int SequenceNumber{ get; set; }

    [JsonProperty("type")]
    public string? Type{ get; set; }

    [JsonProperty("animeIds")]
    public List<string>? AnimeIds{ get; set; }

    [JsonProperty("description")]
    public string? Description{ get; set; }

    [JsonProperty("durationMs")]
    public int DurationMs{ get; set; }

    [JsonProperty("licensor")]
    public string? Licensor{ get; set; }

    [JsonProperty("slug")]
    public string? Slug{ get; set; }

    [JsonProperty("artist")]
    public required MusicVideoArtist Artist{ get; set; }

    [JsonProperty("isPremiumOnly")]
    public bool IsPremiumOnly{ get; set; }

    [JsonProperty("isPublic")]
    public bool IsPublic{ get; set; }

    [JsonProperty("publishDate")]
    public DateTime PublishDate{ get; set; }

    [JsonProperty("displayArtistName")]
    public string? DisplayArtistName{ get; set; }

    [JsonProperty("genres")]
    public object? Genres{ get; set; }

    [JsonProperty("readyToPublish")]
    public bool ReadyToPublish{ get; set; }

    [JsonProperty("id")]
    public required string Id{ get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt{ get; set; }

    public MusicImages? Images{ get; set; }

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt{ get; set; }

    [JsonIgnore]
    public EpisodeType EpisodeType{ get; set; } = EpisodeType.MusicVideo;

    #region Interface

    public string GetSeriesId(){
        return Artist.Id;
    }

    public string GetSeriesTitle(){
        return Artist.Name;
    }

    public string GetSeasonTitle(){
        return EpisodeType == EpisodeType.MusicVideo ? "Music Videos" : "Concerts";
    }

    public string GetSeasonNum(){
        return EpisodeType == EpisodeType.MusicVideo ? "1" : "2";
    }

    public string GetSeasonId(){
        return EpisodeType == EpisodeType.MusicVideo ? "MusicVideos" : "Concerts";
    }

    public string GetEpisodeId(){
        return Id;
    }

    public string GetEpisodeNumber(){
        return SequenceNumber + "";
    }

    public string GetEpisodeTitle(){
        return Title ?? "";
    }

    public string GetEpisodeDescription(){
        return Description ?? "";
    }

    public bool IsSpecialSeason(){
        return false;
    }

    public bool IsSpecialEpisode(){
        return false;
    }

    public List<string> GetAnimeIds(){
        return AnimeIds ?? [];
    }
    
    public List<string> GetEpisodeAvailableDubLang(){
        return[];
    }

    public List<string> GetEpisodeAvailableSoftSubs(){
        return[];
    }

    public DateTime GetAvailableDate(){
        return PublishDate;
    }

    public SeriesType GetSeriesType(){
        return SeriesType.Artist;
    }

    public EpisodeType GetEpisodeType(){
        return EpisodeType;
    }

    #endregion
}

public class MusicImages{
    [JsonProperty("poster_tall")]
    public List<Image> PosterTall{ get; set; } =[];

    [JsonProperty("poster_wide")]
    public List<Image> PosterWide{ get; set; } =[];

    [JsonProperty("promo_image")]
    public List<Image> PromoImage{ get; set; } =[];

    public List<Image> Thumbnail{ get; set; } =[];
}

public class MusicVideoArtist{
    [JsonProperty("id")]
    public required string Id{ get; set; }

    [JsonProperty("name")]
    public required string Name{ get; set; }

    [JsonProperty("slug")]
    public string? Slug{ get; set; }
}

public class MusicVideoAvailability{
    [JsonProperty("endDate")]
    public DateTime EndDate{ get; set; }

    [JsonProperty("startDate")]
    public DateTime StartDate{ get; set; }
}