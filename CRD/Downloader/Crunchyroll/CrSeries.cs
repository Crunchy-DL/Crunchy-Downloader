using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Views;
using ReactiveUI;

namespace CRD.Downloader.Crunchyroll;

public class CrSeries(){
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
                var images = (item.Images?.Thumbnail ?? new List<List<Image>>{ new List<Image>{ new Image{ Source = "/notFound.png" } } });

                Regex dubPattern = new Regex(@"\(\w+ Dub\)");

                var epMeta = new CrunchyEpMeta();
                epMeta.Data = new List<CrunchyEpMetaData>{ new(){ MediaId = item.Id, Versions = item.Versions, IsSubbed = item.IsSubbed, IsDubbed = item.IsDubbed } };
                epMeta.SeriesTitle = episode.Items.FirstOrDefault(a => !dubPattern.IsMatch(a.SeriesTitle)).SeriesTitle ?? Regex.Replace(episode.Items[0].SeriesTitle, @"\(\w+ Dub\)", "").TrimEnd();
                epMeta.SeasonTitle = episode.Items.FirstOrDefault(a => !dubPattern.IsMatch(a.SeasonTitle)).SeasonTitle ?? Regex.Replace(episode.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd();
                epMeta.EpisodeNumber = item.Episode;
                epMeta.EpisodeTitle = item.Title;
                epMeta.SeasonId = item.SeasonId;
                epMeta.Season = Helpers.ExtractNumberAfterS(item.Identifier) ?? item.SeasonNumber + "";
                epMeta.ShowId = item.SeriesId;
                epMeta.AbsolutEpisodeNumberE = epNum;
                epMeta.Image = images[images.Count / 2].FirstOrDefault().Source;
                epMeta.DownloadProgress = new DownloadProgress(){
                    IsDownloading = false,
                    Done = false,
                    Percent = 0,
                    Time = 0,
                    DownloadSpeed = 0
                };
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
                        epMe.Data?.Add(epMetaData);
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

        CrSeriesSearch? parsedSeries = await ParseSeriesById(id, crLocale,forcedLocale);

        if (parsedSeries == null || parsedSeries.Data == null){
            Console.Error.WriteLine("Parse Data Invalid");
            return null;
        }

        // var result = ParseSeriesResult(parsedSeries);
        Dictionary<string, EpisodeAndLanguage> episodes = new Dictionary<string, EpisodeAndLanguage>();
        
        if (crunInstance.CrunOptions.History){
            crunInstance.History.CRUpdateSeries(id,"");
        }

        var cachedSeasonID = "";
        var seasonData = new CrunchyEpisodeList();
        
        foreach (var s in parsedSeries.Data){
                if (data?.S != null && s.Id != data.Value.S) continue;
                int fallbackIndex = 0;
                if (cachedSeasonID != s.Id){
                    seasonData = await GetSeasonDataById(s.Id, forcedLocale ? crLocale : "");
                    cachedSeasonID = s.Id;
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
                                // Ensure there is only one of the same language
                                if (item.Langs.All(a => a.CrLocale != version.AudioLocale)){
                                    // Push to arrays if there are no duplicates of the same language
                                    item.Items.Add(episode);
                                    item.Langs.Add(Array.Find(Languages.languages, a => a.CrLocale == version.AudioLocale));
                                }
                            }
                        } else{
                            // Episode didn't have versions, mark it as such to be logged.
                            serieshasversions = false;
                            // Ensure there is only one of the same language
                            if (item.Langs.All(a => a.CrLocale != episode.AudioLocale)){
                                // Push to arrays if there are no duplicates of the same language
                                item.Items.Add(episode);
                                item.Langs.Add(Array.Find(Languages.languages, a => a.CrLocale == episode.AudioLocale));
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
                newKey = $"SP{specialIndex}_" + item.Items[0].Episode ?? "SP" + (specialIndex + " " + item.Items[0].Id);
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

            var seasonTitle = item.Items.FirstOrDefault(a => !Regex.IsMatch(a.SeasonTitle, @"\(\w+ Dub\)")).SeasonTitle
                              ?? Regex.Replace(item.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd();

            var title = item.Items[0].Title;
            var seasonNumber = Helpers.ExtractNumberAfterS(item.Items[0].Identifier) ?? item.Items[0].SeasonNumber.ToString();

            var languages = item.Items.Select((a, index) =>
                $"{(a.IsPremiumOnly ? "+ " : "")}{item.Langs.ElementAtOrDefault(index).Name ?? "Unknown"}").ToArray(); //☆

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
            var images = (value.Items[0].Images?.Thumbnail ?? new List<List<Image>>{ new List<Image>{ new Image{ Source = "/notFound.png" } } });
            var seconds = (int)Math.Floor(value.Items[0].DurationMs / 1000.0);
            var langList = value.Langs.Select(a => a.CrLocale).ToList();
            Languages.SortListByLangList(langList);
            
            return new Episode{
                E = key.StartsWith("E") ? key.Substring(1) : key,
                Lang = langList,
                Name = value.Items[0].Title,
                Season = Helpers.ExtractNumberAfterS(value.Items[0].Identifier) ?? value.Items[0].SeasonNumber.ToString(),
                SeriesTitle = Regex.Replace(value.Items[0].SeriesTitle, @"\(\w+ Dub\)", "").TrimEnd(),
                SeasonTitle = Regex.Replace(value.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd(),
                EpisodeNum = key.StartsWith("SP") ? key : value.Items[0].EpisodeNumber?.ToString() ?? value.Items[0].Episode ?? "?",
                Id = value.Items[0].SeasonId,
                Img = images[images.Count / 2].FirstOrDefault().Source,
                Description = value.Items[0].Description,
                Time = $"{seconds / 60}:{seconds % 60:D2}" // Ensures two digits for seconds.
            };
        }).ToList();

        return crunchySeriesList;
    }

    public async Task<CrunchyEpisodeList> GetSeasonDataById(string seasonID, string? crLocale, bool forcedLang = false, bool log = false){
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

            var showRequest = HttpClientReq.CreateRequestMessage($"{Api.Cms}/seasons/{seasonID}", HttpMethod.Get, true, true, query);

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

        var episodeRequest = HttpClientReq.CreateRequestMessage($"{Api.Cms}/seasons/{seasonID}/episodes", HttpMethod.Get, true, true, query);

        var episodeRequestResponse = await HttpClientReq.Instance.SendHttpRequest(episodeRequest);

        if (!episodeRequestResponse.IsOk){
            Console.Error.WriteLine($"Episode List Request FAILED! uri: {episodeRequest.RequestUri}");
        } else{
            episodeList = Helpers.Deserialize<CrunchyEpisodeList>(episodeRequestResponse.ResponseContent, crunInstance.SettingsJsonSerializerSettings);
        }

        if (episodeList.Total < 1){
            Console.Error.WriteLine("Season is empty!");
        }

        return episodeList;
    }

    public Dictionary<int, Dictionary<string, SeriesSearchItem>> ParseSeriesResult(CrSeriesSearch seasonsList){
        var ret = new Dictionary<int, Dictionary<string, SeriesSearchItem>>();
        int i = 0;

        foreach (var item in seasonsList.Data){
            i++;
            foreach (var lang in Languages.languages){
                int seasonNumber = item.SeasonNumber;
                if (item.Versions != null){
                    seasonNumber = i;
                }

                if (!ret.ContainsKey(seasonNumber)){
                    ret[seasonNumber] = new Dictionary<string, SeriesSearchItem>();
                }

                if (item.Title.Contains($"({lang.Name} Dub)") || item.Title.Contains($"({lang.Name})")){
                    ret[seasonNumber][lang.Code] = item;
                } else if (item.IsSubbed && !item.IsDubbed && lang.Code == "jpn"){
                    ret[seasonNumber][lang.Code] = item;
                } else if (item.IsDubbed && lang.Code == "eng" && !Languages.languages.Any(a => (item.Title.Contains($"({a.Name})") || item.Title.Contains($"({a.Name} Dub)")))){
                    // Dubbed with no more infos will be treated as eng dubs
                    ret[seasonNumber][lang.Code] = item;
                } else if (item.AudioLocale == lang.CrLocale){
                    ret[seasonNumber][lang.Code] = item;
                }
            }
        }

        return ret;
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


        var request = HttpClientReq.CreateRequestMessage($"{Api.Cms}/series/{id}/seasons", HttpMethod.Get, true, true, query);

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

        var request = HttpClientReq.CreateRequestMessage($"{Api.Cms}/series/{id}", HttpMethod.Get, true, true, query);

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
        query["type"] = "top_results";

        var request = HttpClientReq.CreateRequestMessage($"{Api.Search}", HttpMethod.Get, true, false, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }

        CrSearchSeriesBase? series = Helpers.Deserialize<CrSearchSeriesBase>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        return series;
    }

    public async Task<CrBrowseSeriesBase?> GetAllSeries(string? crLocale){
        CrBrowseSeriesBase? complete = new CrBrowseSeriesBase();
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

            var request = HttpClientReq.CreateRequestMessage($"{Api.Browse}", HttpMethod.Get, true, false, query);

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
}