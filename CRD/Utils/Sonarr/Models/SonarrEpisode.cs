using System;
using Newtonsoft.Json;

namespace CRD.Utils.Sonarr.Models;

public class SonarrEpisode{
    /// <summary>
    /// Gets or sets the series identifier.
    /// </summary>
    /// <value>
    /// The series identifier.
    /// </value>
    [JsonProperty("seriesId")]
    public int SeriesId{ get; set; }

    /// <summary>
    /// Gets or sets the episode file identifier.
    /// </summary>
    /// <value>
    /// The episode file identifier.
    /// </value>
    [JsonProperty("episodeFileId")]
    public int EpisodeFileId{ get; set; }

    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    /// <value>
    /// The season number.
    /// </value>
    [JsonProperty("seasonNumber")]
    public int SeasonNumber{ get; set; }

    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    /// <value>
    /// The episode number.
    /// </value>
    [JsonProperty("episodeNumber")]
    public int EpisodeNumber{ get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    /// <value>
    /// The title.
    /// </value>
    [JsonProperty("title")]
    public string Title{ get; set; }

    /// <summary>
    /// Gets or sets the air date.
    /// </summary>
    /// <value>
    /// The air date.
    /// </value>
    [JsonProperty("airDate")]
    public DateTimeOffset AirDate{ get; set; }

    /// <summary>
    /// Gets or sets the air date UTC.
    /// </summary>
    /// <value>
    /// The air date UTC.
    /// </value>
    [JsonProperty("airDateUtc")]
    public DateTimeOffset AirDateUtc{ get; set; }

    /// <summary>
    /// Gets or sets the overview.
    /// </summary>
    /// <value>
    /// The overview.
    /// </value>
    [JsonProperty("overview")]
    public string Overview{ get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance has file.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has file; otherwise, <c>false</c>.
    /// </value>
    [JsonProperty("hasFile")]
    public bool HasFile{ get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="Episode"/> is monitored.
    /// </summary>
    /// <value>
    ///   <c>true</c> if monitored; otherwise, <c>false</c>.
    /// </value>
    [JsonProperty("monitored")]
    public bool Monitored{ get; set; }

    /// <summary>
    /// Gets or sets the scene episode number.
    /// </summary>
    /// <value>
    /// The scene episode number.
    /// </value>
    [JsonProperty("sceneEpisodeNumber")]
    public int SceneEpisodeNumber{ get; set; }

    /// <summary>
    /// Gets or sets the scene season number.
    /// </summary>
    /// <value>
    /// The scene season number.
    /// </value>
    [JsonProperty("sceneSeasonNumber")]
    public int SceneSeasonNumber{ get; set; }

    /// <summary>
    /// Gets or sets the tv database episode identifier.
    /// </summary>
    /// <value>
    /// The tv database episode identifier.
    /// </value>
    [JsonProperty("tvDbEpisodeId")]
    public int TvDbEpisodeId{ get; set; }

    /// <summary>
    /// Gets or sets the absolute episode number.
    /// </summary>
    /// <value>
    /// The absolute episode number.
    /// </value>
    [JsonProperty("absoluteEpisodeNumber")]
    public int AbsoluteEpisodeNumber{ get; set; }

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    [JsonProperty("id")]
    public int Id{ get; set; }
}