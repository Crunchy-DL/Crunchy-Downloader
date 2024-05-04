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
using Newtonsoft.Json;

namespace CRD.Downloader;

public class CrEpisode(Crunchyroll crunInstance){
    public async Task<CrunchyEpisodeList?> ParseEpisodeById(string id,string locale){
        if (crunInstance.CmsToken?.Cms == null){
            Console.WriteLine("Missing CMS Access Token");
            return null;
        }

        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        query["locale"] = Languages.Locale2language(locale).CrLocale;  

        var request = HttpClientReq.CreateRequestMessage($"{Api.Cms}/episodes/{id}", HttpMethod.Get, true, true, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.WriteLine("Series Request Failed");
            return null;
        }

        CrunchyEpisodeList epsidoe = Helpers.Deserialize<CrunchyEpisodeList>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        if (epsidoe.Total < 1){
            return null;
        }

        return epsidoe;
    }


    public CrunchySeriesList EpisodeData(CrunchyEpisodeList dlEpisodes){
        bool serieshasversions = true;

        Dictionary<string, EpisodeAndLanguage> episodes = new Dictionary<string, EpisodeAndLanguage>();

        if (dlEpisodes.Data != null){
            foreach (var episode in dlEpisodes.Data){
                
                if (crunInstance.CrunOptions.History){
                    crunInstance.CrHistory.UpdateWithEpisode(episode);
                }
                
                // Prepare the episode array
                EpisodeAndLanguage item;
                var seasonIdentifier = !string.IsNullOrEmpty(episode.Identifier) ? episode.Identifier.Split('|')[1] : $"S{episode.SeasonNumber}";
                var episodeKey = $"{seasonIdentifier}E{episode.Episode ?? (episode.EpisodeNumber + "")}";

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

        int specialIndex = 1;
        int epIndex = 1;

        var keys = new List<string>(episodes.Keys); // Copying the keys to a new list to avoid modifying the collection while iterating.

        foreach (var key in keys){
            EpisodeAndLanguage item = episodes[key];
            var isSpecial = !Regex.IsMatch(item.Items[0].Episode ?? string.Empty, @"^\d+$"); // Checking if the episode is not a number (i.e., special).
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

        var specials = episodes.Where(e => e.Key.StartsWith("SP")).ToList();
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

    public Dictionary<string, CrunchyEpMeta> EpisodeMeta(Dictionary<string, EpisodeAndLanguage> eps, List<string> dubLang){
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
                    Error = false,
                    Percent = 0,
                    Time = 0,
                    DownloadSpeed = 0
                };

                var epMetaData = epMeta.Data[0];
                if (!string.IsNullOrEmpty(item.StreamsLink)){
                    epMetaData.Playback = item.StreamsLink;
                    if (string.IsNullOrEmpty(item.Playback)){
                        item.Playback = item.StreamsLink;
                    }
                }

                if (ret.TryGetValue(key, out var epMe)){
                    epMetaData.Lang = episode.Langs[index];
                    epMe.Data?.Add(epMetaData);
                } else{
                    epMetaData.Lang = episode.Langs[index];
                    epMeta.Data[0] = epMetaData;
                    ret.Add(key, epMeta);
                }


                // show ep
                item.SeqId = epNum;
            }
        }


        return ret;
    }
}