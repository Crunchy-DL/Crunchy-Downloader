using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Sonarr.Models;

public class SonarrSeason{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    /// <value>
    /// The season number.
    /// </value>
    [JsonProperty("seasonNumber")] public int SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="Season"/> is monitored.
    /// </summary>
    /// <value>
    ///   <c>true</c> if monitored; otherwise, <c>false</c>.
    /// </value>
    [JsonProperty("monitored")] public bool Monitored { get; set; }

    /// <summary>
    /// Gets or sets the statistics.
    /// </summary>
    /// <value>
    /// The statistics.
    /// </value>
    [JsonProperty("statistics")] public SonarrStatistics Statistics { get; set; }

    /// <summary>
    /// Gets or sets the images.
    /// </summary>
    /// <value>
    /// The images.
    /// </value>
    [JsonProperty("images")] public List<SonarrImage> Images { get; set; }
}