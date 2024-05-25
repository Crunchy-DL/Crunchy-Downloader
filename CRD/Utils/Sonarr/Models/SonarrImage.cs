using Newtonsoft.Json;

namespace CRD.Utils.Sonarr.Models;

public class SonarrImage{
    /// <summary>
    /// Gets or sets the type of the cover.
    /// </summary>
    /// <value>
    /// The type of the cover.
    /// </value>
    [JsonProperty("coverType")] public SonarrCoverType CoverType { get; set; }

    /// <summary>
    /// Gets or sets the URL.
    /// </summary>
    /// <value>
    /// The URL.
    /// </value>
    [JsonProperty("url")] public string Url { get; set; }
}