using System;
using Newtonsoft.Json;

namespace CRD.Utils.Sonarr.Models;

public class SonarrStatistics{
    /// <summary>
    /// Gets or sets the previous airing.
    /// </summary>
    /// <value>
    /// The previous airing.
    /// </value>
    [JsonProperty("previousAiring")]
    public DateTimeOffset PreviousAiring{ get; set; }

    /// <summary>
    /// Gets or sets the episode file count.
    /// </summary>
    /// <value>
    /// The episode file count.
    /// </value>
    [JsonProperty("episodeFileCount")]
    public int EpisodeFileCount{ get; set; }

    /// <summary>
    /// Gets or sets the episode count.
    /// </summary>
    /// <value>
    /// The episode count.
    /// </value>
    [JsonProperty("episodeCount")]
    public int EpisodeCount{ get; set; }

    /// <summary>
    /// Gets or sets the total episode count.
    /// </summary>
    /// <value>
    /// The total episode count.
    /// </value>
    [JsonProperty("totalEpisodeCount")]
    public int TotalEpisodeCount{ get; set; }

    /// <summary>
    /// Gets or sets the size on disk.
    /// </summary>
    /// <value>
    /// The size on disk.
    /// </value>
    [JsonProperty("sizeOnDisk")]
    public long SizeOnDisk{ get; set; }

    /// <summary>
    /// Gets or sets the percent of episodes.
    /// </summary>
    /// <value>
    /// The percent of episodes.
    /// </value>
    [JsonProperty("percentOfEpisodes")]
    public double PercentOfEpisodes{ get; set; }
}