using Newtonsoft.Json;
using YamlDotNet.Core.Tokens;

namespace CRD.Utils.Sonarr.Models;

public class SonarrQualityProfile{

    [JsonProperty("value")]
    public Value Value{ get; set; }


    [JsonProperty("isLoaded")]
    public bool IsLoaded{ get; set; }
}