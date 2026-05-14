using System.Threading;
using System.Threading.Tasks;

namespace CRD.Utils.Notifications.Providers;

public class ExecuteNotificationProvider : INotificationProvider{
    public NotificationProviderType Type => NotificationProviderType.Execute;

    public Task SendAsync(NotificationProviderConfig config, NotificationEvent notificationEvent, CancellationToken cancellationToken = default){
        if (string.IsNullOrWhiteSpace(config.Path)){
            return Task.CompletedTask;
        }

        Helpers.ExecuteFile(config.Path);
        return Task.CompletedTask;
    }
}
