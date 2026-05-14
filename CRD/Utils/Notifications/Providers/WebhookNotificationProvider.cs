using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CRD.Utils.Http;
using Newtonsoft.Json;

namespace CRD.Utils.Notifications.Providers;

public class WebhookNotificationProvider : INotificationProvider{
    public NotificationProviderType Type => NotificationProviderType.Webhook;

    public async Task SendAsync(NotificationProviderConfig config, NotificationEvent notificationEvent, CancellationToken cancellationToken = default){
        if (string.IsNullOrWhiteSpace(config.Url)){
            return;
        }

        using var request = new HttpRequestMessage(new HttpMethod(string.IsNullOrWhiteSpace(config.Method) ? "POST" : config.Method), config.Url);

        foreach (var header in config.Headers){
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var body = BuildBody(config, notificationEvent);
        if (!string.IsNullOrEmpty(body)){
            request.Content = new StringContent(body, Encoding.UTF8, string.IsNullOrWhiteSpace(config.ContentType) ? "application/json" : config.ContentType);
        }

        var response = await HttpClientReq.Instance.SendHttpRequest(request, suppressError: false);
        if (!response.IsOk){
            throw new InvalidOperationException($"Webhook request failed for '{config.Url}'.");
        }
    }

    private static string BuildBody(NotificationProviderConfig config, NotificationEvent notificationEvent){
        if (!string.IsNullOrWhiteSpace(config.BodyTemplate)){
            return ApplyTemplate(config.BodyTemplate, notificationEvent);
        }

        var payload = new{
            eventType = notificationEvent.Type.ToString(),
            title = notificationEvent.Title,
            message = notificationEvent.Message,
            timestampUtc = notificationEvent.TimestampUtc,
            metadata = notificationEvent.Metadata
        };

        return JsonConvert.SerializeObject(payload);
    }

    private static string ApplyTemplate(string template, NotificationEvent notificationEvent){
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase){
            ["eventType"] = notificationEvent.Type.ToString(),
            ["title"] = notificationEvent.Title,
            ["message"] = notificationEvent.Message,
            ["timestampUtc"] = notificationEvent.TimestampUtc.ToString("O")
        };

        foreach (var pair in notificationEvent.Metadata){
            values[pair.Key] = pair.Value;
        }

        foreach (var pair in values){
            template = template.Replace("{{" + pair.Key + "}}", pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return template;
    }
}
