using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CRD.Utils;
using CRD.Utils.Structs;

namespace CRD.Downloader.Crunchyroll.Utils;

public static class DownloadQueueItemFactory{
    private static readonly Regex DubSuffix = new(@"\(\w+ Dub\)", RegexOptions.Compiled);

    public static bool HasDubSuffix(string? s)
        => !string.IsNullOrWhiteSpace(s) && DubSuffix.IsMatch(s);

    public static string StripDubSuffix(string? s)
        => string.IsNullOrWhiteSpace(s) ? "" : DubSuffix.Replace(s, "").TrimEnd();

    public static string CanonicalTitle(IEnumerable<string?> candidates){
        var noDub = candidates.FirstOrDefault(t => !HasDubSuffix(t));
        return !string.IsNullOrWhiteSpace(noDub)
            ? noDub!
            : StripDubSuffix(candidates.FirstOrDefault());
    }

    public static (string small, string big) GetThumbSmallBig(Images? images){
        var firstRow = images?.Thumbnail?.FirstOrDefault();
        var small = firstRow?.FirstOrDefault()?.Source ?? "/notFound.jpg";
        var big = firstRow?.LastOrDefault()?.Source ?? small;
        return (small, big);
    }

    public static CrunchyEpMeta CreateShell(
        StreamingService service,
        string? seriesTitle,
        string? seasonTitle,
        string? episodeNumber,
        string? episodeTitle,
        string? description,
        string? seriesId,
        string? seasonId,
        string? season,
        string? absolutEpisodeNumberE,
        string? image,
        string? imageBig,
        string hslang,
        List<string>? availableSubs = null,
        List<string>? selectedDubs = null,
        bool music = false){
        return new CrunchyEpMeta(){
            SeriesTitle = seriesTitle,
            SeasonTitle = seasonTitle,
            EpisodeNumber = episodeNumber,
            EpisodeTitle = episodeTitle,
            Description = description,

            SeriesId = seriesId,
            SeasonId = seasonId,
            Season = season,
            AbsolutEpisodeNumberE = absolutEpisodeNumberE,

            Image = image,
            ImageBig = imageBig,

            Hslang = hslang,
            AvailableSubs = availableSubs,
            SelectedDubs = selectedDubs,
            Music = music
        };
    }

    public static CrunchyEpMetaData CreateVariant(
        string mediaId,
        LanguageItem? lang,
        string? playback,
        List<EpisodeVersion>? versions,
        bool isSubbed,
        bool isDubbed,
        bool isAudioRoleDescription = false){
        return new CrunchyEpMetaData{
            MediaId = mediaId,
            Lang = lang,
            Playback = playback,
            Versions = versions,
            IsSubbed = isSubbed,
            IsDubbed = isDubbed,
            IsAudioRoleDescription = isAudioRoleDescription
        };
    }
}