using System.Threading;
using System.Threading.Tasks;

namespace CRD.Utils.Notifications;

public interface INotificationProvider{
    NotificationProviderType Type{ get; }

    Task SendAsync(NotificationProviderConfig config, NotificationEvent notificationEvent, CancellationToken cancellationToken = default);
}
