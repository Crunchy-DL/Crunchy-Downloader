using System.Threading;
using System.Threading.Tasks;

namespace CRD.Utils.Notifications.Providers;

public class SoundNotificationProvider : INotificationProvider{
    public NotificationProviderType Type => NotificationProviderType.Sound;

    public Task SendAsync(NotificationProviderConfig config, NotificationEvent notificationEvent, CancellationToken cancellationToken = default){
        if (string.IsNullOrWhiteSpace(config.Path)){
            return Task.CompletedTask;
        }

        var player = new AudioPlayer();
        return player.PlayAsync(config.Path);
    }
}
