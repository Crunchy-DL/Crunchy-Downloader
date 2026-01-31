using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using CRD.Downloader.Crunchyroll.Utils;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;

namespace CRD.Downloader.Crunchyroll;

public class CrEpisode(){
    private readonly CrunchyrollManager crunInstance = CrunchyrollManager.Instance;

    public async Task<CrunchyEpisode?> ParseEpisodeById(string id, string crLocale, bool forcedLang = false){
        await crunInstance.CrAuthGuest.RefreshToken(true);
        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forcedLang){
                query["force_locale"] = crLocale;
            }
        }


        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/episodes/{id}", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }

        CrunchyEpisodeList epsidoe = Helpers.Deserialize<CrunchyEpisodeList>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings) ?? new CrunchyEpisodeList();

        if (epsidoe is{ Total: < 1 }){
            return null;
        }

        if (epsidoe is{ Total: 1, Data: not null } &&
            (epsidoe.Data.First().Versions ?? [])
            .GroupBy(v => v.AudioLocale)
            .Any(g => g.Count() > 1)){
            Console.Error.WriteLine("Episode has Duplicate Audio Locales");
            var list = (epsidoe.Data.First().Versions ?? []).GroupBy(v => v.AudioLocale).Where(g => g.Count() > 1).ToList();
            //guid for episode id
            foreach (var episodeVersionse in list){
                foreach (var version in episodeVersionse){
                    var checkRequest = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/episodes/{version.Guid}", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);
                    var checkResponse = await HttpClientReq.Instance.SendHttpRequest(checkRequest, true);
                    if (!checkResponse.IsOk){
                        epsidoe.Data.First().Versions?.Remove(version);
                    }
                }
            }
        }

        if (epsidoe.Total == 1 && epsidoe.Data != null){
            return epsidoe.Data.First();
        }

        Console.Error.WriteLine("Multiple episodes returned with one ID?");
        if (epsidoe.Data != null) return epsidoe.Data.First();
        return null;
    }


    public async Task<CrunchyRollEpisodeData> EpisodeData(CrunchyEpisode dlEpisode, bool updateHistory = false){
        bool serieshasversions = true;
        var episode = new CrunchyRollEpisodeData();

        if (crunInstance.CrunOptions.History && updateHistory){
            await crunInstance.History.UpdateWithEpisodeList([dlEpisode]);
            var historySeries = crunInstance.HistoryList
                .FirstOrDefault(series => series.SeriesId == dlEpisode.SeriesId);

            if (historySeries != null){
                crunInstance.History.MatchHistorySeriesWithSonarr(false);
                await crunInstance.History.MatchHistoryEpisodesWithSonarr(false, historySeries);
                CfgManager.UpdateHistoryFile();
            }
        }

        // initial key
        var seasonIdentifier = !string.IsNullOrEmpty(dlEpisode.Identifier)
            ? dlEpisode.Identifier.Split('|')[1]
            : $"S{dlEpisode.SeasonNumber}";

        episode.Key = $"{seasonIdentifier}E{dlEpisode.Episode ?? (dlEpisode.EpisodeNumber + "")}";

        episode.EpisodeAndLanguages = new EpisodeAndLanguage();

        // Build Variants
        if (dlEpisode.Versions != null){
            foreach (var version in dlEpisode.Versions){
                var lang = Array.Find(Languages.languages, a => a.CrLocale == version.AudioLocale)
                           ?? Languages.DEFAULT_lang;

                episode.EpisodeAndLanguages.AddUnique(dlEpisode, lang);
            }
        } else{
            serieshasversions = false;

            var lang = Array.Find(Languages.languages, a => a.CrLocale == dlEpisode.AudioLocale)
                       ?? Languages.DEFAULT_lang;

            episode.EpisodeAndLanguages.AddUnique(dlEpisode, lang);
        }

        if (episode.EpisodeAndLanguages.Variants.Count == 0)
            return episode;

        var baseEp = episode.EpisodeAndLanguages.Variants[0].Item;

        var isSpecial = baseEp.IsSpecialEpisode();

        string newKey;
        if (isSpecial && !string.IsNullOrEmpty(baseEp.Episode)){
            newKey = baseEp.Episode;
        } else{
            var epPart = baseEp.Episode ?? (baseEp.EpisodeNumber?.ToString() ?? "1");
            newKey = isSpecial
                ? $"SP{epPart} {baseEp.Id}"
                : $"E{epPart}";
        }

        episode.Key = newKey;

        var seasonTitle =
            episode.EpisodeAndLanguages.Variants
                .Select(v => v.Item.SeasonTitle)
                .FirstOrDefault(t => !DownloadQueueItemFactory.HasDubSuffix(t))
            ?? DownloadQueueItemFactory.StripDubSuffix(baseEp.SeasonTitle);

        var title = baseEp.Title;
        var seasonNumber = baseEp.GetSeasonNum();

        var languages = episode.EpisodeAndLanguages.Variants
            .Select(v => $"{(v.Item.IsPremiumOnly ? "+ " : "")}{v.Lang?.Name ?? "Unknown"}")
            .ToArray();

        Console.WriteLine($"[{episode.Key}] {seasonTitle} - Season {seasonNumber} - {title} [{string.Join(", ", languages)}]");

        if (!serieshasversions)
            Console.WriteLine("Couldn\'t find versions on episode, added languages with language array.");

        return episode;
    }


    public CrunchyEpMeta EpisodeMeta(CrunchyRollEpisodeData episodeP, List<string> dubLang){
        CrunchyEpMeta? retMeta = null;

        var epNum = episodeP.Key.StartsWith('E') ? episodeP.Key[1..] : episodeP.Key;
        var hslang = crunInstance.CrunOptions.Hslang;

        var selectedDubs = dubLang
            .Where(d => episodeP.EpisodeAndLanguages.Variants.Any(v => v.Lang.CrLocale == d))
            .ToList();

        foreach (var v in episodeP.EpisodeAndLanguages.Variants){
            var item = v.Item;
            var lang = v.Lang;

            if (!dubLang.Contains(lang.CrLocale))
                continue;

            item.HideSeasonTitle = true;
            if (string.IsNullOrEmpty(item.SeasonTitle) && !string.IsNullOrEmpty(item.SeriesTitle)){
                item.SeasonTitle = item.SeriesTitle;
                item.HideSeasonTitle = false;
                item.HideSeasonNumber = true;
            }

            if (string.IsNullOrEmpty(item.SeasonTitle) && string.IsNullOrEmpty(item.SeriesTitle)){
                item.SeasonTitle = "NO_TITLE";
                item.SeriesTitle = "NO_TITLE";
            }

            item.SeqId = epNum;

            if (retMeta == null){
                var seriesTitle = DownloadQueueItemFactory.CanonicalTitle(
                    episodeP.EpisodeAndLanguages.Variants.Select(x => (string?)x.Item.SeriesTitle));

                var seasonTitle = DownloadQueueItemFactory.CanonicalTitle(
                    episodeP.EpisodeAndLanguages.Variants.Select(x => (string?)x.Item.SeasonTitle));

                var (img, imgBig) = DownloadQueueItemFactory.GetThumbSmallBig(item.Images);

                retMeta = DownloadQueueItemFactory.CreateShell(
                    service: StreamingService.Crunchyroll,
                    seriesTitle: seriesTitle,
                    seasonTitle: seasonTitle,
                    episodeNumber: item.Episode,
                    episodeTitle: item.GetEpisodeTitle(),
                    description: item.Description,
                    seriesId: item.SeriesId,
                    seasonId: item.SeasonId,
                    season: item.GetSeasonNum(),
                    absolutEpisodeNumberE: epNum,
                    image: img,
                    imageBig: imgBig,
                    hslang: hslang,
                    availableSubs: item.SubtitleLocales,
                    selectedDubs: selectedDubs
                );
            }

            var playback = item.Playback;
            if (!string.IsNullOrEmpty(item.StreamsLink)){
                playback = item.StreamsLink;
                if (string.IsNullOrEmpty(item.Playback))
                    item.Playback = item.StreamsLink;
            }

            retMeta.Data.Add(DownloadQueueItemFactory.CreateVariant(
                mediaId: item.Id,
                lang: lang,
                playback: playback,
                versions: item.Versions,
                isSubbed: item.IsSubbed,
                isDubbed: item.IsDubbed
            ));
        }

        return retMeta ?? new CrunchyEpMeta();
    }

    public async Task<CrBrowseEpisodeBase?> GetNewEpisodes(string? crLocale, int requestAmount, DateTime? firstWeekDay = null, bool forcedLang = false){
        await crunInstance.CrAuthGuest.RefreshToken(true);


        if (string.IsNullOrEmpty(crLocale)){
            crLocale = "en-US";
        }

        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forcedLang){
                query["force_locale"] = crLocale;
            }
        }

        query["n"] = requestAmount + "";
        query["sort_by"] = "newly_added";
        query["type"] = "episode";

        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Browse}", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }

        CrBrowseEpisodeBase? series = Helpers.Deserialize<CrBrowseEpisodeBase>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        series?.Data?.Sort((a, b) =>
            b.EpisodeMetadata.PremiumAvailableDate.CompareTo(a.EpisodeMetadata.PremiumAvailableDate));

        return series;
    }

    public async Task MarkAsWatched(string episodeId){
        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Content}/discover/{crunInstance.CrAuthEndpoint1.Token?.account_id}/mark_as_watched/{episodeId}", HttpMethod.Post, true,
            crunInstance.CrAuthEndpoint1.Token?.access_token, null);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine($"Mark as watched for {episodeId} failed");
        }
    }
}