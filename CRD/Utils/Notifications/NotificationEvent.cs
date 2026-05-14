using System;
using System.Collections.Generic;

namespace CRD.Utils.Notifications;

public class NotificationEvent{
    public NotificationEventType Type{ get; set; }

    public string Title{ get; set; } = string.Empty;

    public string Message{ get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc{ get; set; } = DateTimeOffset.UtcNow;

    public Dictionary<string, string> Metadata{ get; set; } = [];
}
