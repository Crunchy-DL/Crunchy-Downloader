using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Utils.Structs.Crunchyroll;

namespace CRD.Utils.Notifications;

public class NotificationPublisher{
    public static NotificationPublisher Instance{ get; } = new();

    private bool loginExpiredNotificationSent;
    private string notifiedUpdateVersion = string.Empty;

    public Task PublishDownloadFailedAsync(NotificationSettings? settings, CrunchyEpMeta data, string? error = null){
        return NotificationDispatcher.Instance.PublishAsync(settings, new NotificationEvent{
            Type = NotificationEventType.DownloadFailed,
            Title = "Download failed",
            Message = string.IsNullOrWhiteSpace(error)
                ? $"Failed to download {data.SeriesTitle ?? data.EpisodeTitle ?? "item"}."
                : $"Failed to download {data.SeriesTitle ?? data.EpisodeTitle ?? "item"}: {error}",
            Metadata = BuildMetadata(data, error)
        });
    }

    public Task PublishDownloadFinishedAsync(NotificationSettings? settings, CrunchyEpMeta data){
        return NotificationDispatcher.Instance.PublishAsync(settings, new NotificationEvent{
            Type = NotificationEventType.DownloadFinished,
            Title = "Download finished",
            Message = $"Finished processing {data.SeriesTitle ?? data.EpisodeTitle ?? "item"}.",
            Metadata = BuildMetadata(data)
        });
    }

    public Task PublishQueueFinishedAsync(NotificationSettings? settings, CrunchyEpMeta data){
        return NotificationDispatcher.Instance.PublishAsync(settings, new NotificationEvent{
            Type = NotificationEventType.QueueFinished,
            Title = "Downloads finished",
            Message = "All queued downloads have finished processing.",
            Metadata = []
        });
    }

    public async Task PublishLoginExpiredAsync(NotificationSettings? settings, string? username, string? endpoint){
        if (loginExpiredNotificationSent){
            return;
        }

        loginExpiredNotificationSent = true;

        await NotificationDispatcher.Instance.PublishAsync(settings, new NotificationEvent{
            Type = NotificationEventType.LoginExpired,
            Title = "Crunchyroll login expired",
            Message = "The saved Crunchyroll session could not be refreshed. Please log in again.",
            Metadata = new Dictionary<string, string>{
                ["username"] = username ?? string.Empty,
                ["endpoint"] = endpoint ?? string.Empty
            }
        });
    }

    public void ResetLoginExpiredNotification(){
        loginExpiredNotificationSent = false;
    }

    public async Task PublishUpdateAvailableAsync(NotificationSettings? settings, string currentVersion, string latestVersion, string platformName, string downloadUrl){
        if (string.Equals(notifiedUpdateVersion, latestVersion, StringComparison.OrdinalIgnoreCase)){
            return;
        }

        notifiedUpdateVersion = latestVersion;

        await NotificationDispatcher.Instance.PublishAsync(settings, new NotificationEvent{
            Type = NotificationEventType.UpdateAvailable,
            Title = "Update available",
            Message = $"Version {latestVersion} is available. Current version: {currentVersion}.",
            Metadata = new Dictionary<string, string>{
                ["currentVersion"] = currentVersion,
                ["latestVersion"] = latestVersion,
                ["platform"] = platformName,
                ["downloadUrl"] = downloadUrl
            }
        });
    }

    public Task<bool> PublishTrackedSeriesEpisodeReleasedAsync(NotificationSettings? settings, HistorySeries series, HistoryEpisode episode, CrBrowseEpisode? release = null, string? locale = null){
        var episodeUrl = BuildEpisodeUrl(release, episode, locale);
        var imageUrl = release?.Images?.Thumbnail?.FirstOrDefault()?.FirstOrDefault()?.Source
                       ?? episode.ThumbnailImageUrl
                       ?? string.Empty;
        var description = release?.Description
                          ?? episode.EpisodeDescription
                          ?? string.Empty;
        var premiumAvailableDate = release?.EpisodeMetadata?.PremiumAvailableDate;
        var durationMs = release?.EpisodeMetadata?.DurationMs ?? 0;

        return NotificationDispatcher.Instance.PublishWithResultAsync(settings, new NotificationEvent{
            Type = NotificationEventType.TrackedSeriesEpisodeReleased,
            Title = "Tracked series episode released",
            Message = string.IsNullOrWhiteSpace(series.SeriesTitle)
                ? $"A tracked episode is available: {episode.EpisodeTitle ?? episode.EpisodeId ?? "Unknown episode"}."
                : $"A tracked episode is available for {series.SeriesTitle}: {episode.EpisodeTitle ?? episode.EpisodeId ?? "Unknown episode"}.",
            Metadata = new Dictionary<string, string>{
                ["seriesTitle"] = series.SeriesTitle ?? string.Empty,
                ["seriesId"] = series.SeriesId ?? string.Empty,
                ["seasonId"] = release?.EpisodeMetadata?.SeasonId ?? string.Empty,
                ["episodeTitle"] = episode.EpisodeTitle ?? string.Empty,
                ["episodeId"] = episode.EpisodeId ?? string.Empty,
                ["episodeNumber"] = episode.Episode ?? string.Empty,
                ["seasonNumber"] = episode.EpisodeSeasonNum ?? string.Empty,
                ["releaseDate"] = episode.EpisodeCrPremiumAirDate?.ToString("O") ?? string.Empty,
                ["premiumAvailableDate"] = premiumAvailableDate?.ToString("O") ?? episode.EpisodeCrPremiumAirDate?.ToString("O") ?? string.Empty,
                ["episodeUrl"] = episodeUrl,
                ["imageUrl"] = imageUrl,
                ["description"] = description,
                ["durationMs"] = durationMs > 0 ? durationMs.ToString() : string.Empty,
                ["availableDubs"] = string.Join(", ", episode.HistoryEpisodeAvailableDubLang ?? []),
                ["availableSubs"] = string.Join(", ", episode.HistoryEpisodeAvailableSoftSubs ?? [])
            }
        });
    }

    public void ResetUpdateAvailableNotification(){
        notifiedUpdateVersion = string.Empty;
    }

    private static Dictionary<string, string> BuildMetadata(CrunchyEpMeta data, string? error = null){
        var metadata = new Dictionary<string, string>{
            ["seriesTitle"] = data.SeriesTitle ?? string.Empty,
            ["seasonTitle"] = data.SeasonTitle ?? string.Empty,
            ["episodeTitle"] = data.EpisodeTitle ?? string.Empty,
            ["episodeNumber"] = data.EpisodeNumber ?? string.Empty,
            ["episodeId"] = data.EpisodeId ?? string.Empty,
            ["downloadPath"] = data.DownloadPath ?? string.Empty,
            ["seasonNumber"] = data.Season ?? string.Empty,
            ["description"] = data.Description ?? string.Empty,
            ["imageUrl"] = data.Image ?? string.Empty,
            ["imageUrlLarge"] = data.ImageBig ?? string.Empty,
            ["downloadSubs"] = string.Join(", ", data.DownloadSubs ?? []),
            ["downloadDubs"] = string.Join(", ", data.SelectedDubs ?? []),
            ["hardsub"] = data.Hslang ?? string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(data.SeriesId)){
            metadata["seriesId"] = data.SeriesId;
        }

        if (!string.IsNullOrWhiteSpace(data.SeasonId)){
            metadata["seasonId"] = data.SeasonId;
        }

        if (!string.IsNullOrWhiteSpace(data.EpisodeId)){
            metadata["episodeUrl"] = $"https://www.crunchyroll.com/watch/{data.EpisodeId}";
        }

        if (!string.IsNullOrWhiteSpace(error)){
            metadata["error"] = error;
        }

        return metadata;
    }

    private static string BuildEpisodeUrl(CrBrowseEpisode? release, HistoryEpisode episode, string? locale){
        var episodeId = release?.Id ?? episode.EpisodeId;
        if (string.IsNullOrWhiteSpace(episodeId)){
            return string.Empty;
        }

        var normalizedLocale = string.IsNullOrWhiteSpace(locale) ? "en-US" : locale;
        var slugTitle = release?.SlugTitle;

        return string.IsNullOrWhiteSpace(slugTitle)
            ? $"https://www.crunchyroll.com/{normalizedLocale}/watch/{episodeId}"
            : $"https://www.crunchyroll.com/{normalizedLocale}/watch/{episodeId}/{slugTitle}";
    }
}
