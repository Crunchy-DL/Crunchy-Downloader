using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace CRD.Utils.Notifications;

public class NotificationSettings{
    [JsonProperty("providers")]
    public List<NotificationProviderConfig> Providers{ get; set; } = [];

    public NotificationProviderConfig GetOrCreateProvider(NotificationProviderType type){
        var provider = Providers.FirstOrDefault(p => p.Type == type);
        if (provider != null){
            provider.Events ??= [];
            provider.Headers ??= [];
            return provider;
        }

        provider = new NotificationProviderConfig{
            Type = type
        };
        Providers.Add(provider);
        return provider;
    }
}
