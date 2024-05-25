using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Sonarr.Models;

public class SonarrSeries{
    /// <summary>
    /// Gets or sets the TVDB identifier.
    /// </summary>
    /// <value>
    /// The TVDB identifier.
    /// </value>
    [JsonProperty("tvdbId")]
    public int TvdbId{ get; set; }

    /// <summary>
    /// Gets or sets the tv rage identifier.
    /// </summary>
    /// <value>
    /// The tv rage identifier.
    /// </value>
    [JsonProperty("tvRageId")]
    public long TvRageId{ get; set; }

    /// <summary>
    /// Gets or sets the imdb identifier.
    /// </summary>
    /// <value>
    /// The imdb identifier.
    /// </value>
    [JsonProperty("imdbId")]
    public string ImdbId{ get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    /// <value>
    /// The title.
    /// </value>
    [JsonProperty("title")]
    public string Title{ get; set; }

    /// <summary>
    /// Gets or sets the clean title.
    /// </summary>
    /// <value>
    /// The clean title.
    /// </value>
    [JsonProperty("cleanTitle")]
    public string CleanTitle{ get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    /// <value>
    /// The status.
    /// </value>
    [JsonProperty("status")]
    public SonarrStatus Status{ get; set; }

    /// <summary>
    /// Gets or sets the overview.
    /// </summary>
    /// <value>
    /// The overview.
    /// </value>
    [JsonProperty("overview")]
    public string Overview{ get; set; }

    /// <summary>
    /// Gets or sets the air time.
    /// </summary>
    /// <value>
    /// The air time.
    /// </value>
    [JsonProperty("airTime")]
    public string AirTime{ get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="Series"/> is monitored.
    /// </summary>
    /// <value>
    ///   <c>true</c> if monitored; otherwise, <c>false</c>.
    /// </value>
    [JsonProperty("monitored")]
    public bool Monitored{ get; set; }

    /// <summary>
    /// Gets or sets the quality profile identifier.
    /// </summary>
    /// <value>
    /// The quality profile identifier.
    /// </value>
    [JsonProperty("qualityProfileId")]
    public long QualityProfileId{ get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether [season folder].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [season folder]; otherwise, <c>false</c>.
    /// </value>
    [JsonProperty("seasonFolder")]
    public bool SeasonFolder{ get; set; }

    /// <summary>
    /// Gets or sets the last information synchronize.
    /// </summary>
    /// <value>
    /// The last information synchronize.
    /// </value>
    [JsonProperty("lastInfoSync")]
    public DateTimeOffset LastInfoSync{ get; set; }

    /// <summary>
    /// Gets or sets the runtime.
    /// </summary>
    /// <value>
    /// The runtime.
    /// </value>
    [JsonProperty("runtime")]
    public long Runtime{ get; set; }

    /// <summary>
    /// Gets or sets the images.
    /// </summary>
    /// <value>
    /// The images.
    /// </value>
    [JsonProperty("images")]
    public List<SonarrImage> Images{ get; set; }

    /// <summary>
    /// Gets or sets the type of the series.
    /// </summary>
    /// <value>
    /// The type of the series.
    /// </value>
    [JsonProperty("seriesType")]
    public SonarrSeriesType SeriesType{ get; set; }

    /// <summary>
    /// Gets or sets the network.
    /// </summary>
    /// <value>
    /// The network.
    /// </value>
    [JsonProperty("network")]
    public string Network{ get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether [use scene numbering].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [use scene numbering]; otherwise, <c>false</c>.
    /// </value>
    [JsonProperty("useSceneNumbering")]
    public bool UseSceneNumbering{ get; set; }

    /// <summary>
    /// Gets or sets the title slug.
    /// </summary>
    /// <value>
    /// The title slug.
    /// </value>
    [JsonProperty("titleSlug")]
    public string TitleSlug{ get; set; }

    /// <summary>
    /// Gets or sets the path.
    /// </summary>
    /// <value>
    /// The path.
    /// </value>
    [JsonProperty("path")]
    public string Path{ get; set; }

    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    /// <value>
    /// The year.
    /// </value>
    [JsonProperty("year")]
    public int Year{ get; set; }

    /// <summary>
    /// Gets or sets the first aired.
    /// </summary>
    /// <value>
    /// The first aired.
    /// </value>
    [JsonProperty("firstAired")]
    public DateTimeOffset FirstAired{ get; set; }

    /// <summary>
    /// Gets or sets the quality profile.
    /// </summary>
    /// <value>
    /// The quality profile.
    /// </value>
    [JsonProperty("qualityProfile")]
    public SonarrQualityProfile QualityProfile{ get; set; }

    /// <summary>
    /// Gets or sets the seasons.
    /// </summary>
    /// <value>
    /// The seasons.
    /// </value>
    [JsonProperty("seasons")]
    public List<SonarrSeason> Seasons{ get; set; }

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    [JsonProperty("id")]
    public int Id{ get; set; }
}