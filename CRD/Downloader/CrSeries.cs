using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using CRD.Utils;
using CRD.Utils.Structs;
using Newtonsoft.Json;

namespace CRD.Downloader;

public class CrSeries(Crunchyroll crunInstance){
    public async Task<List<CrunchyEpMeta>> DownloadFromSeriesId(string id, CrunchyMultiDownload data){
        var series = await ListSeriesId(id, "" ,data);

        if (series != null){
            var selected = ItemSelectMultiDub(series.Value.Data, data.DubLang, data.But, data.AllEpisodes, data.E);

            foreach (var crunchyEpMeta in selected.Values){
                if (crunchyEpMeta.Data == null) continue;
                var languages = crunchyEpMeta.Data.Select((a) =>
                    $" {a.Lang?.Name ?? "Unknown Language"}");

                Console.WriteLine($"[S{crunchyEpMeta.Season}E{crunchyEpMeta.EpisodeNumber} - {crunchyEpMeta.EpisodeTitle} [{string.Join(", ", languages)}]");
            }

            return selected.Values.ToList();
        }

        return new List<CrunchyEpMeta>();
    }

    public Dictionary<string, CrunchyEpMeta> ItemSelectMultiDub(Dictionary<string, EpisodeAndLanguage> eps, List<string> dubLang, bool? but, bool? all, List<string>? e){
        var ret = new Dictionary<string, CrunchyEpMeta>();


        foreach (var kvp in eps){
            var key = kvp.Key;
            var episode = kvp.Value;

            for (int index = 0; index < episode.Items.Count; index++){
                var item = episode.Items[index];

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
                epMeta.Season = item.SeasonNumber;
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


    public async Task<CrunchySeriesList?> ListSeriesId(string id,string Locale, CrunchyMultiDownload? data){
        await crunInstance.CrAuth.RefreshToken(true);

        bool serieshasversions = true;

        CrSeriesSearch? parsedSeries = await ParseSeriesById(id,Locale); // one piece - GRMG8ZQZR

        if (parsedSeries == null){
            Console.WriteLine("Parse Data Invalid");
            return null;
        }

        var result = ParseSeriesResult(parsedSeries);
        Dictionary<string, EpisodeAndLanguage> episodes = new Dictionary<string, EpisodeAndLanguage>();


        foreach (int season in result.Keys){
            foreach (var key in result[season].Keys){
                var s = result[season][key];
                if (data?.S != null && s.Id != data.Value.S) continue;
                int fallbackIndex = 0;
                var seasonData = await GetSeasonDataById(s.Id);
                if (seasonData.Data != null){

                    if (crunInstance.CrunOptions.History){
                        crunInstance.CrHistory.UpdateWithSeasonData(seasonData);
                    }
                    
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
        }

        int specialIndex = 1;
        int epIndex = 1;

        var keys = new List<string>(episodes.Keys); // Copying the keys to a new list to avoid modifying the collection while iterating.

        foreach (var key in keys){
            EpisodeAndLanguage item = episodes[key];
            var episode = item.Items[0].Episode;
            var isSpecial = episode != null && !Regex.IsMatch(episode, @"^\d+$"); // Checking if the episode is not a number (i.e., special).
            // var newKey = $"{(isSpecial ? 'S' : 'E')}{(isSpecial ? specialIndex : epIndex).ToString()}";

            string newKey;
            if (isSpecial && !string.IsNullOrEmpty(item.Items[0].Episode)){
                newKey = item.Items[0].Episode ?? "SP" + (specialIndex + " " + item.Items[0].Id);
            } else{
                newKey = $"{(isSpecial ? "SP" : 'E')}{(isSpecial ? (specialIndex + " " + item.Items[0].Id) : epIndex + "")}";
            }

            episodes.Remove(key);
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
            var seasonNumber = item.Items[0].SeasonNumber;

            var languages = item.Items.Select((a, index) =>
                $"{(a.IsPremiumOnly ? "+ " : "")}{item.Langs.ElementAtOrDefault(index).Name ?? "Unknown"}").ToArray(); //☆

            Console.WriteLine($"[{key}] {seasonTitle} - Season {seasonNumber} - {title} [{string.Join(", ", languages)}]");
        }

        if (!serieshasversions){
            Console.WriteLine("Couldn\'t find versions on some episodes, fell back to old method.");
        }

        CrunchySeriesList crunchySeriesList = new CrunchySeriesList();
        crunchySeriesList.Data = sortedEpisodes;

        crunchySeriesList.List = sortedEpisodes.Select(kvp => {
            var key = kvp.Key;
            var value = kvp.Value;
            var images = (value.Items[0].Images?.Thumbnail ?? new List<List<Image>>{ new List<Image>{ new Image{ Source = "/notFound.png" } } });
            var seconds = (int)Math.Floor(value.Items[0].DurationMs / 1000.0);
            return new Episode{
                E = key.StartsWith("E") ? key.Substring(1) : key,
                Lang = value.Langs.Select(a => a.Code).ToList(),
                Name = value.Items[0].Title,
                Season = value.Items[0].SeasonNumber.ToString(),
                SeriesTitle = Regex.Replace(value.Items[0].SeriesTitle, @"\(\w+ Dub\)", "").TrimEnd(),
                SeasonTitle = Regex.Replace(value.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd(),
                EpisodeNum = value.Items[0].EpisodeNumber?.ToString() ?? value.Items[0].Episode ?? "?",
                Id = value.Items[0].SeasonId,
                Img = images[images.Count / 2].FirstOrDefault().Source,
                Description = value.Items[0].Description,
                Time = $"{seconds / 60}:{seconds % 60:D2}" // Ensures two digits for seconds.
            };
        }).ToList();

        return crunchySeriesList;
    }

    public async Task<CrunchyEpisodeList> GetSeasonDataById(string seasonID, bool log = false){
        CrunchyEpisodeList episodeList = new CrunchyEpisodeList(){ Data = new List<CrunchyEpisode>(), Total = 0, Meta = new Meta() };

        if (crunInstance.CmsToken?.Cms == null){
            Console.WriteLine("Missing CMS Token");
            return episodeList;
        }

        if (log){
            var showRequest = HttpClientReq.CreateRequestMessage($"{Api.Cms}/seasons/{seasonID}?preferred_audio_language=ja-JP", HttpMethod.Get, true, true, null);

            var response = await HttpClientReq.Instance.SendHttpRequest(showRequest);

            if (!response.IsOk){
                Console.WriteLine("Show Request FAILED!");
            } else{
                Console.WriteLine(response.ResponseContent);
            }
        }

        //TODO

        var episodeRequest = new HttpRequestMessage(HttpMethod.Get, $"{Api.Cms}/seasons/{seasonID}/episodes?preferred_audio_language=ja-JP");

        episodeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", crunInstance.Token?.access_token);

        var episodeRequestResponse = await HttpClientReq.Instance.SendHttpRequest(episodeRequest);

        if (!episodeRequestResponse.IsOk){
            Console.WriteLine($"Episode List Request FAILED! uri: {episodeRequest.RequestUri}");
        } else{
            episodeList = Helpers.Deserialize<CrunchyEpisodeList>(episodeRequestResponse.ResponseContent, crunInstance.SettingsJsonSerializerSettings);
        }

        if (episodeList.Total < 1){
            Console.WriteLine("Season is empty!");
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
    
    public async Task<CrSeriesSearch?> ParseSeriesById(string id,string? locale){
        if (crunInstance.CmsToken?.Cms == null){
            Console.WriteLine("Missing CMS Access Token");
            return null;
        }

        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(locale)){
            query["locale"] = Languages.Locale2language(locale).CrLocale;  
        }
       

        var request = HttpClientReq.CreateRequestMessage($"{Api.Cms}/series/{id}/seasons", HttpMethod.Get, true, true, query);
        
        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.WriteLine("Series Request Failed");
            return null;
        }


        CrSeriesSearch? seasonsList = Helpers.Deserialize<CrSeriesSearch>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        if (seasonsList == null || seasonsList.Total < 1){
            return null;
        }

        return seasonsList;
    }
    
    public async Task<CrSeriesBase?> SeriesById(string id){
        if (crunInstance.CmsToken?.Cms == null){
            Console.WriteLine("Missing CMS Access Token");
            return null;
        }

        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";

        var request = HttpClientReq.CreateRequestMessage($"{Api.Cms}/series/{id}", HttpMethod.Get, true, true, query);
        
        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.WriteLine("Series Request Failed");
            return null;
        }


        CrSeriesBase? series = Helpers.Deserialize<CrSeriesBase>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        if (series == null || series.Total < 1){
            return null;
        }

        return series;
    }
    
}