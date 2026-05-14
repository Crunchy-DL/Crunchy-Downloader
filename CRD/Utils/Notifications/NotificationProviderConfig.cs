using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Notifications;

public class NotificationProviderConfig{
    [JsonProperty("type")]
    public NotificationProviderType Type{ get; set; }

    [JsonProperty("enabled")]
    public bool Enabled{ get; set; }

    [JsonProperty("events")]
    public List<NotificationEventType> Events{ get; set; } = [];

    [JsonProperty("path")]
    public string Path{ get; set; } = string.Empty;

    [JsonProperty("url")]
    public string Url{ get; set; } = string.Empty;

    [JsonProperty("method")]
    public string Method{ get; set; } = "POST";

    [JsonProperty("headers")]
    public Dictionary<string, string> Headers{ get; set; } = [];

    [JsonProperty("content_type")]
    public string ContentType{ get; set; } = "application/json";

    [JsonProperty("body_template")]
    public string BodyTemplate{ get; set; } = string.Empty;

    public bool Handles(NotificationEventType eventType){
        return Events.Count == 0 || Events.Contains(eventType);
    }
}
