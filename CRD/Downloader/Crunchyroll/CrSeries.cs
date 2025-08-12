﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.Views;
using ReactiveUI;

namespace CRD.Downloader.Crunchyroll;

public class CrSeries{
    private readonly CrunchyrollManager crunInstance = CrunchyrollManager.Instance;

    public Dictionary<string, CrunchyEpMeta> ItemSelectMultiDub(Dictionary<string, EpisodeAndLanguage> eps, List<string> dubLang, bool? but, bool? all, List<string>? e){
        var ret = new Dictionary<string, CrunchyEpMeta>();


        foreach (var kvp in eps){
            var key = kvp.Key;
            var episode = kvp.Value;

            for (int index = 0; index < episode.Items.Count; index++){
                var item = episode.Items[index];

                if (item.IsPremiumOnly && !crunInstance.Profile.HasPremium){
                    MessageBus.Current.SendMessage(new ToastMessage($"Episode is a premium episode – make sure that you are signed in with an account that has an active premium subscription", ToastType.Error, 3));
                    continue;
                }

                if (crunInstance.CrunOptions.History){
                    var dubLangList = crunInstance.History.GetDubList(item.SeriesId, item.SeasonId);
                    if (dubLangList.Count > 0){
                        dubLang = dubLangList;
                    }
                }

                if (!dubLang.Contains(episode.Langs[index].CrLocale))
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

                var epNum = key.StartsWith('E') ? key[1..] : key;
                var images = (item.Images?.Thumbnail ??[new List<Image>{ new(){ Source = "/notFound.jpg" } }]);

                Regex dubPattern = new Regex(@"\(\w+ Dub\)");

                var epMeta = new CrunchyEpMeta();
                epMeta.Data = new List<CrunchyEpMetaData>{ new(){ MediaId = item.Id, Versions = item.Versions, IsSubbed = item.IsSubbed, IsDubbed = item.IsDubbed } };
                epMeta.SeriesTitle = episode.Items.FirstOrDefault(a => !dubPattern.IsMatch(a.SeriesTitle))?.SeriesTitle ?? Regex.Replace(episode.Items[0].SeriesTitle, @"\(\w+ Dub\)", "").TrimEnd();
                epMeta.SeasonTitle = episode.Items.FirstOrDefault(a => !dubPattern.IsMatch(a.SeasonTitle))?.SeasonTitle ?? Regex.Replace(episode.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd();
                epMeta.EpisodeNumber = item.Episode;
                epMeta.EpisodeTitle = item.Title;
                epMeta.SeasonId = item.SeasonId;
                epMeta.Season = Helpers.ExtractNumberAfterS(item.Identifier) ?? item.SeasonNumber + "";
                epMeta.SeriesId = item.SeriesId;
                epMeta.AbsolutEpisodeNumberE = epNum;
                epMeta.Image = images.FirstOrDefault()?.FirstOrDefault()?.Source ?? string.Empty;
                epMeta.ImageBig = images.FirstOrDefault()?.LastOrDefault()?.Source ?? string.Empty;
                epMeta.DownloadProgress = new DownloadProgress(){
                    IsDownloading = false,
                    Done = false,
                    Percent = 0,
                    Time = 0,
                    DownloadSpeed = 0
                };
                epMeta.Hslang = CrunchyrollManager.Instance.CrunOptions.Hslang;
                epMeta.Description = item.Description;
                epMeta.AvailableSubs = item.SubtitleLocales;
                if (episode.Langs.Count > 0){
                    epMeta.SelectedDubs = dubLang
                        .Where(language => episode.Langs.Any(epLang => epLang.CrLocale == language))
                        .ToList();
                }


                var epMetaData = epMeta.Data[0];
                if (!string.IsNullOrEmpty(item.StreamsLink)){
                    epMetaData.Playback = item.StreamsLink;
                    if (string.IsNullOrEmpty(item.Playback)){
                        item.Playback = item.StreamsLink;
                    }
                }

                if (all is true || e != null && e.Contains(epNum)){
                    if (ret.TryGetValue(key, out var epMe)){
                        epMetaData.Lang = episode.Langs[index];
                        epMe.Data.Add(epMetaData);
                    } else{
                        epMetaData.Lang = episode.Langs[index];
                        epMeta.Data[0] = epMetaData;
                        ret.Add(key, epMeta);
                    }
                }


                // show ep
                item.SeqId = epNum;
            }
        }


        return ret;
    }


    public async Task<CrunchySeriesList?> ListSeriesId(string id, string crLocale, CrunchyMultiDownload? data, bool forcedLocale = false){
        await crunInstance.CrAuth.RefreshToken(true);

        bool serieshasversions = true;

        CrSeriesSearch? parsedSeries = await ParseSeriesById(id, crLocale, forcedLocale);

        if (parsedSeries == null || parsedSeries.Data == null){
            Console.Error.WriteLine("Parse Data Invalid");
            return null;
        }

        // var result = ParseSeriesResult(parsedSeries);
        Dictionary<string, EpisodeAndLanguage> episodes = new Dictionary<string, EpisodeAndLanguage>();

        if (crunInstance.CrunOptions.History){
            _ = crunInstance.History.CrUpdateSeries(id, "");
        }

        var cachedSeasonId = "";
        var seasonData = new CrunchyEpisodeList();

        foreach (var s in parsedSeries.Data){
            if (data?.S != null && s.Id != data.S) continue;
            int fallbackIndex = 0;
            if (cachedSeasonId != s.Id){
                seasonData = await GetSeasonDataById(s.Id, forcedLocale ? crLocale : "");
                cachedSeasonId = s.Id;
            }

            if (seasonData.Data != null){
                foreach (var episode in seasonData.Data){
                    // Prepare the episode array
                    EpisodeAndLanguage item;


                    string episodeNum = (episode.Episode != String.Empty ? episode.Episode : (episode.EpisodeNumber != null ? episode.EpisodeNumber + "" : $"F{fallbackIndex++}")) ?? string.Empty;

                    var seasonIdentifier = !string.IsNullOrEmpty(s.Identifier) ? s.Identifier.Split('|')[1] : $"S{episode.SeasonNumber}";
                    var episodeKey = $"{seasonIdentifier}E{episodeNum}";

                    if (!episodes.ContainsKey(episodeKey)){
                        item = new EpisodeAndLanguage{
                            Items = new List<CrunchyEpisode>(),
                            Langs = new List<LanguageItem>()
                        };
                        episodes[episodeKey] = item;
                    } else{
                        item = episodes[episodeKey];
                    }

                    if (episode.Versions != null){
                        foreach (var version in episode.Versions){
                            if (item.Langs.All(a => a.CrLocale != version.AudioLocale)){
                                item.Items.Add(episode);
                                item.Langs.Add(Array.Find(Languages.languages, a => a.CrLocale == version.AudioLocale) ?? new LanguageItem());
                            }
                        }
                    } else{
                        serieshasversions = false;
                        if (item.Langs.All(a => a.CrLocale != episode.AudioLocale)){
                            item.Items.Add(episode);
                            item.Langs.Add(Array.Find(Languages.languages, a => a.CrLocale == episode.AudioLocale) ?? new LanguageItem());
                        }
                    }
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

        var keys = new List<string>(episodes.Keys); // Copying the keys to a new list to avoid modifying the collection while iterating.

        foreach (var key in keys){
            EpisodeAndLanguage item = episodes[key];
            var episode = item.Items[0].Episode;
            var isSpecial = episode != null && !Regex.IsMatch(episode, @"^\d+(\.\d+)?$"); // Checking if the episode is not a number (i.e., special).
            // var newKey = $"{(isSpecial ? 'S' : 'E')}{(isSpecial ? specialIndex : epIndex).ToString()}";

            string newKey;
            if (isSpecial && !string.IsNullOrEmpty(item.Items[0].Episode)){
                newKey = $"SP{specialIndex}_" + item.Items[0].Episode;// ?? "SP" + (specialIndex + " " + item.Items[0].Id);
            } else{
                newKey = $"{(isSpecial ? "SP" : 'E')}{(isSpecial ? (specialIndex + " " + item.Items[0].Id) : epIndex + "")}";
            }


            episodes.Remove(key);

            int counter = 1;
            string originalKey = newKey;
            while (episodes.ContainsKey(newKey)){
                newKey = originalKey + "_" + counter;
                counter++;
            }

            episodes.Add(newKey, item);

            if (isSpecial){
                specialIndex++;
            } else{
                epIndex++;
            }
        }

        var specials = episodes.Where(e => e.Key.StartsWith("S")).ToList();
        var normal = episodes.Where(e => e.Key.StartsWith("E")).ToList();

        // Combining and sorting episodes with normal first, then specials.
        var sortedEpisodes = new Dictionary<string, EpisodeAndLanguage>(normal.Concat(specials));

        foreach (var kvp in sortedEpisodes){
            var key = kvp.Key;
            var item = kvp.Value;

            var seasonTitle = item.Items.FirstOrDefault(a => !Regex.IsMatch(a.SeasonTitle, @"\(\w+ Dub\)"))?.SeasonTitle
                              ?? Regex.Replace(item.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd();

            var title = item.Items[0].Title;
            var seasonNumber = Helpers.ExtractNumberAfterS(item.Items[0].Identifier) ?? item.Items[0].SeasonNumber.ToString();

            var languages = item.Items.Select((a, index) =>
                $"{(a.IsPremiumOnly ? "+ " : "")}{item.Langs.ElementAtOrDefault(index)?.Name ?? "Unknown"}").ToArray(); //☆

            Console.WriteLine($"[{key}] {seasonTitle} - Season {seasonNumber} - {title} [{string.Join(", ", languages)}]");
        }

        if (!serieshasversions){
            Console.WriteLine("Couldn\'t find versions on some episodes, added languages with language array.");
        }

        CrunchySeriesList crunchySeriesList = new CrunchySeriesList();
        crunchySeriesList.Data = sortedEpisodes;

        crunchySeriesList.List = sortedEpisodes.Select(kvp => {
            var key = kvp.Key;
            var value = kvp.Value;
            var images = (value.Items.FirstOrDefault()?.Images?.Thumbnail ??[new List<Image>{ new(){ Source = "/notFound.jpg" } }]);
            var seconds = (int)Math.Floor((value.Items.FirstOrDefault()?.DurationMs ?? 0) / 1000.0);
            var langList = value.Langs.Select(a => a.CrLocale).ToList();
            Languages.SortListByLangList(langList);

            return new Episode{
                E = key.StartsWith("E") ? key.Substring(1) : key,
                Lang = langList,
                Name = value.Items.FirstOrDefault()?.Title ?? string.Empty,
                Season = (Helpers.ExtractNumberAfterS(value.Items.FirstOrDefault()?.Identifier?? string.Empty) ?? value.Items.FirstOrDefault()?.SeasonNumber.ToString()) ?? string.Empty,
                SeriesTitle = Regex.Replace(value.Items.FirstOrDefault()?.SeriesTitle?? string.Empty, @"\(\w+ Dub\)", "").TrimEnd(),
                SeasonTitle = Regex.Replace(value.Items.FirstOrDefault()?.SeasonTitle?? string.Empty, @"\(\w+ Dub\)", "").TrimEnd(),
                EpisodeNum = key.StartsWith("SP") ? key : value.Items.FirstOrDefault()?.EpisodeNumber?.ToString() ?? value.Items.FirstOrDefault()?.Episode ?? "?",
                Id = value.Items.FirstOrDefault()?.SeasonId ?? string.Empty,
                Img = images.FirstOrDefault()?.FirstOrDefault()?.Source ?? string.Empty,
                Description = value.Items.FirstOrDefault()?.Description ?? string.Empty,
                EpisodeType = EpisodeType.Episode,
                Time = $"{seconds / 60}:{seconds % 60:D2}" // Ensures two digits for seconds.
            };
        }).ToList();

        return crunchySeriesList;
    }

    public async Task<CrunchyEpisodeList> GetSeasonDataById(string seasonId, string? crLocale, bool forcedLang = false, bool log = false){
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

            var showRequest = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/seasons/{seasonId}", HttpMethod.Get, true, true, query);

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

        var episodeRequest = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/seasons/{seasonId}/episodes", HttpMethod.Get, true, true, query);

        var episodeRequestResponse = await HttpClientReq.Instance.SendHttpRequest(episodeRequest);

        if (!episodeRequestResponse.IsOk){
            Console.Error.WriteLine($"Episode List Request FAILED! uri: {episodeRequest.RequestUri}");
        } else{
            episodeList = Helpers.Deserialize<CrunchyEpisodeList>(episodeRequestResponse.ResponseContent, crunInstance.SettingsJsonSerializerSettings) ??
                          new CrunchyEpisodeList(){ Data =[], Total = 0, Meta = new Meta() };
        }

        if (episodeList.Total < 1){
            Console.Error.WriteLine("Season is empty!");
        }

        return episodeList;
    }

    public async Task<CrSeriesSearch?> ParseSeriesById(string id, string? crLocale, bool forced = false){
        await crunInstance.CrAuth.RefreshToken(true);
        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forced){
                query["force_locale"] = crLocale;
            }
        }


        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/series/{id}/seasons", HttpMethod.Get, true, true, query);

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
        await crunInstance.CrAuth.RefreshToken(true);
        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forced){
                query["force_locale"] = crLocale;
            }
        }

        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/series/{id}", HttpMethod.Get, true, true, query);

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
        await crunInstance.CrAuth.RefreshToken(true);
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

        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Search}", HttpMethod.Get, true, false, query);

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
        CrBrowseSeriesBase complete = new CrBrowseSeriesBase();
        complete.Data =[];

        var i = 0;

        do{
            NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

            if (!string.IsNullOrEmpty(crLocale)){
                query["locale"] = crLocale;
            }

            query["start"] = i + "";
            query["n"] = "50";
            query["sort_by"] = "alphabetical";

            var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Browse}", HttpMethod.Get, true, false, query);

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
        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
        }
        
        query["seasonal_tag"] = season.ToLower() + "-" + year;
        query["n"] = "100";

        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Browse}", HttpMethod.Get, true, false, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }

        CrBrowseSeriesBase? series = Helpers.Deserialize<CrBrowseSeriesBase>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        return series;
    }
    
}