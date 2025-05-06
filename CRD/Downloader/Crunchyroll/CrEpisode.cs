using System;
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

namespace CRD.Downloader.Crunchyroll;

public class CrEpisode(){
    private readonly CrunchyrollManager crunInstance = CrunchyrollManager.Instance;

    public async Task<CrunchyEpisode?> ParseEpisodeById(string id, string crLocale, bool forcedLang = false){
        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forcedLang){
                query["force_locale"] = crLocale;
            }
        }


        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/episodes/{id}", HttpMethod.Get, true, true, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }

        CrunchyEpisodeList epsidoe = Helpers.Deserialize<CrunchyEpisodeList>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings) ?? new CrunchyEpisodeList();

        if (epsidoe is{ Total: < 1 }){
            return null;
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

        // Dictionary<string, EpisodeAndLanguage> episodes = new Dictionary<string, EpisodeAndLanguage>();

        CrunchyRollEpisodeData episode = new CrunchyRollEpisodeData();

        if (crunInstance.CrunOptions.History && updateHistory){
            await crunInstance.History.UpdateWithEpisodeList([dlEpisode]);
            var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == dlEpisode.SeriesId);
            if (historySeries != null){
                CrunchyrollManager.Instance.History.MatchHistorySeriesWithSonarr(false);
                await CrunchyrollManager.Instance.History.MatchHistoryEpisodesWithSonarr(false, historySeries);
                CfgManager.UpdateHistoryFile();
            }
        }

        var seasonIdentifier = !string.IsNullOrEmpty(dlEpisode.Identifier) ? dlEpisode.Identifier.Split('|')[1] : $"S{dlEpisode.SeasonNumber}";
        episode.Key = $"{seasonIdentifier}E{dlEpisode.Episode ?? (dlEpisode.EpisodeNumber + "")}";
        episode.EpisodeAndLanguages = new EpisodeAndLanguage{
            Items = new List<CrunchyEpisode>(),
            Langs = new List<LanguageItem>()
        };

        if (dlEpisode.Versions != null){
            foreach (var version in dlEpisode.Versions){
                // Ensure there is only one of the same language
                if (episode.EpisodeAndLanguages.Langs.All(a => a.CrLocale != version.AudioLocale)){
                    // Push to arrays if there are no duplicates of the same language
                    episode.EpisodeAndLanguages.Items.Add(dlEpisode);
                    episode.EpisodeAndLanguages.Langs.Add(Array.Find(Languages.languages, a => a.CrLocale == version.AudioLocale) ?? Languages.DEFAULT_lang);
                }
            }
        } else{
            // Episode didn't have versions, mark it as such to be logged.
            serieshasversions = false;
            // Ensure there is only one of the same language
            if (episode.EpisodeAndLanguages.Langs.All(a => a.CrLocale != dlEpisode.AudioLocale)){
                // Push to arrays if there are no duplicates of the same language
                episode.EpisodeAndLanguages.Items.Add(dlEpisode);
                episode.EpisodeAndLanguages.Langs.Add(Array.Find(Languages.languages, a => a.CrLocale == dlEpisode.AudioLocale) ?? Languages.DEFAULT_lang);
            }
        }


        int specialIndex = 1;
        int epIndex = 1;


        var isSpecial = !Regex.IsMatch(episode.EpisodeAndLanguages.Items[0].Episode ?? string.Empty, @"^\d+(\.\d+)?$"); // Checking if the episode is not a number (i.e., special).
        string newKey;
        if (isSpecial && !string.IsNullOrEmpty(episode.EpisodeAndLanguages.Items[0].Episode)){
            newKey = episode.EpisodeAndLanguages.Items[0].Episode ?? "SP" + (specialIndex + " " + episode.EpisodeAndLanguages.Items[0].Id);
        } else{
            newKey = $"{(isSpecial ? "SP" : 'E')}{(isSpecial ? (specialIndex + " " + episode.EpisodeAndLanguages.Items[0].Id) : episode.EpisodeAndLanguages.Items[0].Episode ?? epIndex + "")}";
        }

        episode.Key = newKey;

        var seasonTitle = episode.EpisodeAndLanguages.Items.FirstOrDefault(a => !Regex.IsMatch(a.SeasonTitle, @"\(\w+ Dub\)"))?.SeasonTitle
                          ?? Regex.Replace(episode.EpisodeAndLanguages.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd();

        var title = episode.EpisodeAndLanguages.Items[0].Title;
        var seasonNumber = Helpers.ExtractNumberAfterS(episode.EpisodeAndLanguages.Items[0].Identifier) ?? episode.EpisodeAndLanguages.Items[0].SeasonNumber.ToString();

        var languages = episode.EpisodeAndLanguages.Items.Select((a, index) =>
            $"{(a.IsPremiumOnly ? "+ " : "")}{episode.EpisodeAndLanguages.Langs.ElementAtOrDefault(index)?.Name ?? "Unknown"}").ToArray(); //☆

        Console.WriteLine($"[{episode.Key}] {seasonTitle} - Season {seasonNumber} - {title} [{string.Join(", ", languages)}]");


        if (!serieshasversions){
            Console.WriteLine("Couldn\'t find versions on episode, added languages with language array.");
        }

        return episode;
    }

    public CrunchyEpMeta EpisodeMeta(CrunchyRollEpisodeData episodeP, List<string> dubLang){
        // var ret = new Dictionary<string, CrunchyEpMeta>();

        var retMeta = new CrunchyEpMeta();


        for (int index = 0; index < episodeP.EpisodeAndLanguages.Items.Count; index++){
            var item = episodeP.EpisodeAndLanguages.Items[index];

            if (!dubLang.Contains(episodeP.EpisodeAndLanguages.Langs[index].CrLocale))
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

            var epNum = episodeP.Key.StartsWith('E') ? episodeP.Key[1..] : episodeP.Key;
            var images = (item.Images?.Thumbnail ?? new List<List<Image>>{ new List<Image>{ new Image{ Source = "/notFound.png" } } });

            Regex dubPattern = new Regex(@"\(\w+ Dub\)");

            var epMeta = new CrunchyEpMeta();
            epMeta.Data = new List<CrunchyEpMetaData>{ new(){ MediaId = item.Id, Versions = item.Versions, IsSubbed = item.IsSubbed, IsDubbed = item.IsDubbed } };
            epMeta.SeriesTitle = episodeP.EpisodeAndLanguages.Items.FirstOrDefault(a => !dubPattern.IsMatch(a.SeriesTitle))?.SeriesTitle ??
                                 Regex.Replace(episodeP.EpisodeAndLanguages.Items[0].SeriesTitle, @"\(\w+ Dub\)", "").TrimEnd();
            epMeta.SeasonTitle = episodeP.EpisodeAndLanguages.Items.FirstOrDefault(a => !dubPattern.IsMatch(a.SeasonTitle))?.SeasonTitle ??
                                 Regex.Replace(episodeP.EpisodeAndLanguages.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd();
            epMeta.EpisodeNumber = item.Episode;
            epMeta.EpisodeTitle = item.Title;
            epMeta.SeasonId = item.SeasonId;
            epMeta.Season = Helpers.ExtractNumberAfterS(item.Identifier) ?? item.SeasonNumber + "";
            epMeta.SeriesId = item.SeriesId;
            epMeta.AbsolutEpisodeNumberE = epNum;
            epMeta.Image = images[images.Count / 2].FirstOrDefault()?.Source;
            epMeta.DownloadProgress = new DownloadProgress(){
                IsDownloading = false,
                Done = false,
                Error = false,
                Percent = 0,
                Time = 0,
                DownloadSpeed = 0
            };
            epMeta.AvailableSubs = item.SubtitleLocales;
            epMeta.Description = item.Description;
            epMeta.Hslang = CrunchyrollManager.Instance.CrunOptions.Hslang;

            if (episodeP.EpisodeAndLanguages.Langs.Count > 0){
                epMeta.SelectedDubs = dubLang
                    .Where(language => episodeP.EpisodeAndLanguages.Langs.Any(epLang => epLang.CrLocale == language))
                    .ToList();
            }

            var epMetaData = epMeta.Data[0];
            if (!string.IsNullOrEmpty(item.StreamsLink)){
                epMetaData.Playback = item.StreamsLink;
                if (string.IsNullOrEmpty(item.Playback)){
                    item.Playback = item.StreamsLink;
                }
            }

            if (retMeta.Data is{ Count: > 0 }){
                epMetaData.Lang = episodeP.EpisodeAndLanguages.Langs[index];
                retMeta.Data.Add(epMetaData);
            } else{
                epMetaData.Lang = episodeP.EpisodeAndLanguages.Langs[index];
                epMeta.Data[0] = epMetaData;
                retMeta = epMeta;
            }


            // show ep
            item.SeqId = epNum;
        }


        return retMeta;
    }

    public async Task<CrBrowseEpisodeBase?> GetNewEpisodes(string? crLocale, int requestAmount, DateTime? firstWeekDay = null, bool forcedLang = false){
        await crunInstance.CrAuth.RefreshToken(true);
        CrBrowseEpisodeBase? complete = new CrBrowseEpisodeBase();
        complete.Data =[];

        var i = 0;

        do{
            NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

            if (!string.IsNullOrEmpty(crLocale)){
                query["locale"] = crLocale;
                if (forcedLang){
                    query["force_locale"] = crLocale;
                }
            }

            query["start"] = i + "";
            query["n"] = "50";
            query["sort_by"] = "newly_added";
            query["type"] = "episode";

            var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Browse}", HttpMethod.Get, true, false, query);

            var response = await HttpClientReq.Instance.SendHttpRequest(request);

            if (!response.IsOk){
                Console.Error.WriteLine("Series Request Failed");
                return null;
            }

            CrBrowseEpisodeBase? series = Helpers.Deserialize<CrBrowseEpisodeBase>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

            if (series != null){
                complete.Total = series.Total;
                if (series.Data != null){
                    complete.Data.AddRange(series.Data);
                    if (firstWeekDay != null){
                        if (firstWeekDay.Value.Date <= series.Data.Last().LastPublic && i + 50 == requestAmount){
                            requestAmount += 50;
                        }
                    }
                }
            } else{
                break;
            }

            i += 50;
        } while (i < requestAmount && requestAmount < 500);


        return complete;
    }
    
    public async Task MarkAsWatched(string episodeId){
        
        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Content}/discover/{crunInstance.Token?.account_id}/mark_as_watched/{episodeId}", HttpMethod.Post, true,false,null);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine($"Mark as watched for {episodeId} failed");
        }
        
    }
    
    
}