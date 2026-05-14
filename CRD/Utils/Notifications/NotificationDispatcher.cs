using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRD.Utils.Notifications.Providers;

namespace CRD.Utils.Notifications;

public class NotificationDispatcher{
    public static NotificationDispatcher Instance{ get; } = new();

    private readonly IReadOnlyDictionary<NotificationProviderType, INotificationProvider> providers;

    private NotificationDispatcher(){
        var providerInstances = new INotificationProvider[]{
            new SoundNotificationProvider(),
            new ExecuteNotificationProvider(),
            new WebhookNotificationProvider()
        };

        providers = providerInstances.ToDictionary(provider => provider.Type);
    }

    public async Task PublishAsync(NotificationSettings? settings, NotificationEvent notificationEvent, CancellationToken cancellationToken = default){
        await PublishWithResultAsync(settings, notificationEvent, cancellationToken);
    }

    public async Task<bool> PublishWithResultAsync(NotificationSettings? settings, NotificationEvent notificationEvent, CancellationToken cancellationToken = default){
        if (settings?.Providers == null || settings.Providers.Count == 0){
            return false;
        }

        var sentSuccessfully = false;

        foreach (var config in settings.Providers.Where(provider => provider.Enabled && provider.Handles(notificationEvent.Type))){
            if (!providers.TryGetValue(config.Type, out var provider)){
                continue;
            }

            try{
                await provider.SendAsync(config, notificationEvent, cancellationToken);
                sentSuccessfully = true;
            } catch (Exception exception){
                Console.Error.WriteLine($"Failed to send {config.Type} notification: {exception}");
            }
        }

        return sentSuccessfully;
    }
}
