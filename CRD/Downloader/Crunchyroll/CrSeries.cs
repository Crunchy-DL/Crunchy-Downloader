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
using CRD.Utils.Http;
using CRD.Utils.Structs;
using CRD.Views;
using ReactiveUI;

namespace CRD.Downloader.Crunchyroll;

public class CrSeries{
    private readonly CrunchyrollManager crunInstance = CrunchyrollManager.Instance;

    public Dictionary<string, CrunchyEpMeta> ItemSelectMultiDub(Dictionary<string, EpisodeAndLanguage> eps, List<string> dubLang, bool? all, List<string>? e){
        var ret = new Dictionary<string, CrunchyEpMeta>();

        var hasPremium = crunInstance.CrAuthEndpoint1.Profile.HasPremium;

        var hslang = crunInstance.CrunOptions.Hslang;

        bool ShouldInclude(string epNum) =>
            all is true || (e != null && e.Contains(epNum));

        foreach (var (key, episode) in eps){
            var epNum = key.StartsWith('E') ? key[1..] : key;

            foreach (var v in episode.Variants){
                var item = v.Item;
                var lang = v.Lang;

                item.SeqId = epNum;

                if (item.IsPremiumOnly && !hasPremium){
                    MessageBus.Current.SendMessage(new ToastMessage(
                        "Episode is a premium episode – make sure that you are signed in with an account that has an active premium subscription",
                        ToastType.Error, 3));
                    continue;
                }

                // history override
                var effectiveDubs = dubLang;
                if (crunInstance.CrunOptions.History){
                    var dubLangList = crunInstance.History.GetDubList(item.SeriesId, item.SeasonId);
                    if (dubLangList.Count > 0)
                        effectiveDubs = dubLangList;
                }

                if (!effectiveDubs.Contains(lang.CrLocale))
                    continue;

                // season title fallbacks (same behavior)
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

                // selection gate
                if (!ShouldInclude(epNum))
                    continue;

                // Create base queue item once per "key"
                if (!ret.TryGetValue(key, out var qItem)){
                    var seriesTitle = DownloadQueueItemFactory.CanonicalTitle(
                        episode.Variants.Select(x => (string?)x.Item.SeriesTitle));

                    var seasonTitle = DownloadQueueItemFactory.CanonicalTitle(
                        episode.Variants.Select(x => (string?)x.Item.SeasonTitle));

                    var (img, imgBig) = DownloadQueueItemFactory.GetThumbSmallBig(item.Images);

                    var selectedDubs = effectiveDubs
                        .Where(d => episode.Variants.Any(x => x.Lang.CrLocale == d))
                        .ToList();

                    qItem = DownloadQueueItemFactory.CreateShell(
                        service: StreamingService.Crunchyroll,
                        seriesTitle: seriesTitle,
                        seasonTitle: seasonTitle,
                        episodeNumber: item.Episode,
                        episodeTitle: item.Title,
                        description: item.Description,
                        episodeId: item.Id,
                        seriesId: item.SeriesId,
                        seasonId: item.SeasonId,
                        season: Helpers.ExtractNumberAfterS(item.Identifier) ?? item.SeasonNumber.ToString(),
                        absolutEpisodeNumberE: epNum,
                        image: img,
                        imageBig: imgBig,
                        hslang: hslang,
                        availableSubs: item.SubtitleLocales,
                        selectedDubs: selectedDubs
                    );

                    ret.Add(key, qItem);
                }

                // playback preference
                var playback = item.Playback;
                if (!string.IsNullOrEmpty(item.StreamsLink)){
                    playback = item.StreamsLink;
                    if (string.IsNullOrEmpty(item.Playback))
                        item.Playback = item.StreamsLink;
                }

                // Add variant
                ret[key].Data.Add(DownloadQueueItemFactory.CreateVariant(
                    mediaId: item.Id,
                    lang: lang,
                    playback: playback,
                    versions: item.Versions,
                    isSubbed: item.IsSubbed,
                    isDubbed: item.IsDubbed
                ));
            }
        }

        return ret;
    }


    public async Task<CrunchySeriesList?> ListSeriesId(string id, string crLocale, CrunchyMultiDownload? data, bool forcedLocale = false){
        bool serieshasversions = true;

        CrSeriesSearch? parsedSeries = await ParseSeriesById(id, crLocale, forcedLocale);

        if (parsedSeries?.Data == null){
            Console.Error.WriteLine("Parse Data Invalid");
            return null;
        }

        var episodes = new Dictionary<string, EpisodeAndLanguage>();

        if (crunInstance.CrunOptions.History)
            _ = crunInstance.History.CrUpdateSeries(id, "");

        var cachedSeasonId = "";
        var seasonData = new CrunchyEpisodeList();

        foreach (var s in parsedSeries.Data){
            if (data?.S != null && s.Id != data.S)
                continue;

            int fallbackIndex = 0;

            if (cachedSeasonId != s.Id){
                seasonData = await GetSeasonDataById(s.Id, forcedLocale ? crLocale : "");
                cachedSeasonId = s.Id;
            }

            if (seasonData.Data == null)
                continue;

            foreach (var episode in seasonData.Data){
                string episodeNum =
                    (episode.Episode != string.Empty ? episode.Episode : (episode.EpisodeNumber != null ? episode.EpisodeNumber + "" : $"F{fallbackIndex++}"))
                    ?? string.Empty;

                var seasonIdentifier = !string.IsNullOrEmpty(s.Identifier)
                    ? s.Identifier.Split('|')[1]
                    : $"S{episode.SeasonNumber}";

                var episodeKey = $"{seasonIdentifier}E{episodeNum}";

                if (!episodes.TryGetValue(episodeKey, out var item)){
                    item = new EpisodeAndLanguage(); // must have Variants
                    episodes[episodeKey] = item;
                }

                if (episode.Versions != null){
                    foreach (var version in episode.Versions){
                        var lang = Array.Find(Languages.languages, a => a.CrLocale == version.AudioLocale) ?? new LanguageItem();
                        item.AddUnique(episode, lang); // must enforce uniqueness by CrLocale
                    }
                } else{
                    serieshasversions = false;
                    var lang = Array.Find(Languages.languages, a => a.CrLocale == episode.AudioLocale) ?? new LanguageItem();
                    item.AddUnique(episode, lang);
                }
            }
        }

        if (crunInstance.CrunOptions.History){
            var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == id);
            if (historySeries != null){
                crunInstance.History.MatchHistorySeriesWithSonarr(false);
                await crunInstance.History.MatchHistoryEpisodesWithSonarr(false, historySeries);
                CfgManager.UpdateHistoryFile();
            }
        }

        int specialIndex = 1;
        int epIndex = 1;

        var keys = new List<string>(episodes.Keys);

        foreach (var key in keys){
            var item = episodes[key];
            if (item.Variants.Count == 0)
                continue;

            var baseEp = item.Variants[0].Item;

            var epStr = baseEp.Episode;
            var isSpecial = epStr != null && !Regex.IsMatch(epStr, @"^\d+(\.\d+)?$");

            string newKey;
            if (isSpecial && !string.IsNullOrEmpty(baseEp.Episode)){
                newKey = $"SP{specialIndex}_" + baseEp.Episode;
            } else{
                newKey = $"{(isSpecial ? "SP" : 'E')}{(isSpecial ? (specialIndex + " " + baseEp.Id) : epIndex + "")}";
            }

            episodes.Remove(key);

            int counter = 1;
            string originalKey = newKey;
            while (episodes.ContainsKey(newKey)){
                newKey = originalKey + "_" + counter;
                counter++;
            }

            episodes.Add(newKey, item);

            if (isSpecial) specialIndex++;
            else epIndex++;
        }

        var normal = episodes.Where(kvp => kvp.Key.StartsWith("E")).ToList();
        var specials = episodes.Where(kvp => kvp.Key.StartsWith("SP")).ToList();

        var sortedEpisodes = new Dictionary<string, EpisodeAndLanguage>(normal.Concat(specials));

        foreach (var kvp in sortedEpisodes){
            var key = kvp.Key;
            var item = kvp.Value;

            if (item.Variants.Count == 0)
                continue;

            var baseEp = item.Variants[0].Item;

            var seasonTitle = DownloadQueueItemFactory.CanonicalTitle(
                item.Variants.Select(string? (v) => v.Item.SeasonTitle)
            );

            var title = baseEp.Title;
            var seasonNumber = Helpers.ExtractNumberAfterS(baseEp.Identifier) ?? baseEp.SeasonNumber.ToString();

            var languages = item.Variants
                .Select(v => $"{(v.Item.IsPremiumOnly ? "+ " : "")}{v.Lang?.Name ?? "Unknown"}")
                .ToArray();

            Console.WriteLine($"[{key}] {seasonTitle} - Season {seasonNumber} - {title} [{string.Join(", ", languages)}]");
        }

        if (!serieshasversions)
            Console.WriteLine("Couldn\'t find versions on some episodes, added languages with language array.");

        var crunchySeriesList = new CrunchySeriesList{
            Data = sortedEpisodes
        };

        crunchySeriesList.List = sortedEpisodes.Select(kvp => {
            var key = kvp.Key;
            var value = kvp.Value;

            if (value.Variants.Count == 0){
                return new Episode{
                    E = key.StartsWith("E") ? key.Substring(1) : key,
                    Lang = new List<string>(),
                    Name = string.Empty,
                    Season = string.Empty,
                    SeriesTitle = string.Empty,
                    SeasonTitle = string.Empty,
                    EpisodeNum = key,
                    Id = string.Empty,
                    Img = string.Empty,
                    Description = string.Empty,
                    EpisodeType = EpisodeType.Episode,
                    Time = "0:00"
                };
            }

            var baseEp = value.Variants[0].Item;

            var thumbRow = baseEp.Images.Thumbnail.FirstOrDefault();
            var img = thumbRow?.FirstOrDefault()?.Source ?? "/notFound.jpg";

            var seconds = (int)Math.Floor((baseEp.DurationMs) / 1000.0);

            var langList = value.Variants
                .Select(v => v.Lang.CrLocale)
                .Distinct()
                .ToList();

            Languages.SortListByLangList(langList);

            return new Episode{
                E = key.StartsWith("E") ? key.Substring(1) : key,
                Lang = langList,
                Name = baseEp.Title ?? string.Empty,
                Season = (Helpers.ExtractNumberAfterS(baseEp.Identifier) ?? baseEp.SeasonNumber.ToString()) ?? string.Empty,
                SeriesTitle = DownloadQueueItemFactory.StripDubSuffix(baseEp.SeriesTitle),
                SeasonTitle = DownloadQueueItemFactory.StripDubSuffix(baseEp.SeasonTitle),
                EpisodeNum = key.StartsWith("SP")
                    ? key
                    : (baseEp.EpisodeNumber?.ToString() ?? baseEp.Episode ?? "?"),
                Id = baseEp.SeasonId ?? string.Empty,
                Img = img,
                Description = baseEp.Description ?? string.Empty,
                EpisodeType = EpisodeType.Episode,
                Time = $"{seconds / 60}:{seconds % 60:D2}"
            };
        }).ToList();

        return crunchySeriesList;
    }

    public async Task<CrunchyEpisodeList> GetSeasonDataById(string seasonId, string? crLocale, bool forcedLang = false, bool log = false){
        await crunInstance.CrAuthGuest.RefreshToken(true);
        CrunchyEpisodeList episodeList = new CrunchyEpisodeList(){ Data = new List<CrunchyEpisode>(), Total = 0, Meta = new Meta() };

        NameValueCollection query;
        if (log){
            query = HttpUtility.ParseQueryString(new UriBuilder().Query);

            query["preferred_audio_language"] = "ja-JP";
            if (!string.IsNullOrEmpty(crLocale)){
                query["locale"] = crLocale;
                if (forcedLang){
                    query["force_locale"] = crLocale;
                }
            }

            var showRequest = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/seasons/{seasonId}", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);

            var response = await HttpClientReq.Instance.SendHttpRequest(showRequest);

            if (!response.IsOk){
                Console.Error.WriteLine("Show Request FAILED!");
            } else{
                Console.WriteLine(response.ResponseContent);
            }
        }

        query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forcedLang){
                query["force_locale"] = crLocale;
            }
        }

        var episodeRequest = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/seasons/{seasonId}/episodes", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);

        var episodeRequestResponse = await HttpClientReq.Instance.SendHttpRequest(episodeRequest);

        if (!episodeRequestResponse.IsOk){
            Console.Error.WriteLine($"Episode List Request FAILED! uri: {episodeRequest.RequestUri}");
        } else{
            episodeList = Helpers.Deserialize<CrunchyEpisodeList>(episodeRequestResponse.ResponseContent, crunInstance.SettingsJsonSerializerSettings) ??
                          new CrunchyEpisodeList(){ Data = [], Total = 0, Meta = new Meta() };
        }

        if (episodeList.Total < 1){
            Console.Error.WriteLine($"Season is empty! Uri: {episodeRequest.RequestUri}");
        }

        return episodeList;
    }

    public async Task<CrSeriesSearch?> ParseSeriesById(string id, string? crLocale, bool forced = false){
        await crunInstance.CrAuthGuest.RefreshToken(true);
        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forced){
                query["force_locale"] = crLocale;
            }
        }


        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/series/{id}/seasons", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }


        CrSeriesSearch? seasonsList = Helpers.Deserialize<CrSeriesSearch>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        if (seasonsList == null || seasonsList.Total < 1){
            return null;
        }

        return seasonsList;
    }

    public async Task<CrSeriesBase?> SeriesById(string id, string? crLocale, bool forced = false){
        await crunInstance.CrAuthGuest.RefreshToken(true);


        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forced){
                query["force_locale"] = crLocale;
            }
        }

        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/series/{id}", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }


        CrSeriesBase? series = Helpers.Deserialize<CrSeriesBase>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        if (series == null || series.Total < 1){
            return null;
        }

        return series;
    }


    public async Task<CrSearchSeriesBase?> Search(string searchString, string? crLocale, bool forced = false){
        await crunInstance.CrAuthGuest.RefreshToken(true);

        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forced){
                query["force_locale"] = crLocale;
            }
        }

        query["q"] = searchString;
        query["n"] = "6";
        query["type"] = "series";

        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Search}", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }

        CrSearchSeriesBase? series = Helpers.Deserialize<CrSearchSeriesBase>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        if (crunInstance.CrunOptions.History){
            var historyIDs = new HashSet<string>(crunInstance.HistoryList.Select(item => item.SeriesId ?? ""));

            if (series?.Data != null){
                foreach (var crSearchSeries in series.Data){
                    if (crSearchSeries.Items != null){
                        foreach (var crBrowseSeries in crSearchSeries.Items.Where(crBrowseSeries => historyIDs.Contains(crBrowseSeries.Id ?? "unknownID"))){
                            crBrowseSeries.IsInHistory = true;
                        }
                    }
                }
            }
        }

        return series;
    }

    public async Task<CrBrowseSeriesBase?> GetAllSeries(string? crLocale){
        await crunInstance.CrAuthGuest.RefreshToken(true);
        CrBrowseSeriesBase complete = new CrBrowseSeriesBase();
        complete.Data = [];

        var i = 0;

        do{
            NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

            if (!string.IsNullOrEmpty(crLocale)){
                query["locale"] = crLocale;
            }

            query["start"] = i + "";
            query["n"] = "50";
            query["sort_by"] = "alphabetical";

            var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Browse}", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);

            var response = await HttpClientReq.Instance.SendHttpRequest(request);

            if (!response.IsOk){
                Console.Error.WriteLine("Series Request Failed");
                return null;
            }

            CrBrowseSeriesBase? series = Helpers.Deserialize<CrBrowseSeriesBase>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

            if (series != null){
                complete.Total = series.Total;
                if (series.Data != null) complete.Data.AddRange(series.Data);
            } else{
                break;
            }

            i += 50;
        } while (i < complete.Total);


        return complete;
    }

    public async Task<CrBrowseSeriesBase?> GetSeasonalSeries(string season, string year, string? crLocale){
        await crunInstance.CrAuthGuest.RefreshToken(true);
        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
        }

        query["seasonal_tag"] = season.ToLower() + "-" + year;
        query["n"] = "100";

        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Browse}", HttpMethod.Get, true, crunInstance.CrAuthGuest.Token?.access_token, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }

        CrBrowseSeriesBase? series = Helpers.Deserialize<CrBrowseSeriesBase>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        return series;
    }
}