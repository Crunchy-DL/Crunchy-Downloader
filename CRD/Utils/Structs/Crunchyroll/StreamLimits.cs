using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CRD.Utils.Structs.Crunchyroll;

public class StreamError{
    [JsonPropertyName("error")]
    public string Error{ get; set; }

    [JsonPropertyName("activeStreams")]
    public List<ActiveStream> ActiveStreams{ get; set; } = new ();

    public static StreamError? FromJson(string json){
        try{
            return Helpers.Deserialize<StreamError>(json,null);
        } catch (Exception e){
            Console.Error.WriteLine(e);
            return null;
        }
    }

    public bool IsTooManyActiveStreamsError(){
        return Error == "TOO_MANY_ACTIVE_STREAMS";
    }
}

public class ActiveStream{
    [JsonPropertyName("deviceSubtype")]
    public string DeviceSubtype{ get; set; }

    [JsonPropertyName("accountId")]
    public string AccountId{ get; set; }

    [JsonPropertyName("deviceType")]
    public string DeviceType{ get; set; }

    [JsonPropertyName("subscription")]
    public string Subscription{ get; set; }

    [JsonPropertyName("maxKeepAliveSeconds")]
    public int MaxKeepAliveSeconds{ get; set; }

    [JsonPropertyName("ttl")]
    public int Ttl{ get; set; }

    [JsonPropertyName("episodeIdentity")]
    public string EpisodeIdentity{ get; set; }

    [JsonPropertyName("tabId")]
    public string TabId{ get; set; }

    [JsonPropertyName("country")]
    public string Country{ get; set; }

    [JsonPropertyName("clientId")]
    public string ClientId{ get; set; }

    [JsonPropertyName("active")]
    public bool Active{ get; set; }

    [JsonPropertyName("deviceId")]
    public string DeviceId{ get; set; }

    [JsonPropertyName("token")]
    public string Token{ get; set; }

    [JsonPropertyName("assetId")]
    public string AssetId{ get; set; }

    [JsonPropertyName("sessionType")]
    public string SessionType{ get; set; }

    [JsonPropertyName("contentId")]
    public string ContentId{ get; set; }

    [JsonPropertyName("usesStreamLimits")]
    public bool UsesStreamLimits{ get; set; }

    [JsonPropertyName("playbackType")]
    public string PlaybackType{ get; set; }

    [JsonPropertyName("pk")]
    public string Pk{ get; set; }

    [JsonPropertyName("id")]
    public string Id{ get; set; }

    [JsonPropertyName("createdTimestamp")]
    public long CreatedTimestamp{ get; set; }

    [JsonPropertyName("lastKeepAliveTimestamp")]
    public long LastKeepAliveTimestamp{ get; set; }
}