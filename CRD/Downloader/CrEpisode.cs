using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Remote.Protocol.Input;
using CRD.Utils;
using CRD.Utils.Structs;
using Newtonsoft.Json;

namespace CRD.Downloader;

public class CrEpisode(){
    private readonly Crunchyroll crunInstance = Crunchyroll.Instance;

    public async Task<CrunchyEpisode?> ParseEpisodeById(string id, string crLocale,bool forcedLang = false){
        if (crunInstance.CmsToken?.Cms == null){
            Console.Error.WriteLine("Missing CMS Access Token");
            return null;
        }

        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);
        
        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forcedLang){
                query["force_locale"] = crLocale;   
            }
        }
        

        var request = HttpClientReq.CreateRequestMessage($"{Api.Cms}/episodes/{id}", HttpMethod.Get, true, true, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Series Request Failed");
            return null;
        }

        CrunchyEpisodeList epsidoe = Helpers.Deserialize<CrunchyEpisodeList>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

        if (epsidoe.Total < 1){
            return null;
        }

        if (epsidoe.Total == 1 && epsidoe.Data != null){
            return epsidoe.Data.First();
        }

        Console.Error.WriteLine("Multiple episodes returned with one ID?");
        if (epsidoe.Data != null) return epsidoe.Data.First();
        return null;
    }


    public async Task<CrunchyRollEpisodeData> EpisodeData(CrunchyEpisode dlEpisode){
        bool serieshasversions = true;

        // Dictionary<string, EpisodeAndLanguage> episodes = new Dictionary<string, EpisodeAndLanguage>();

        CrunchyRollEpisodeData episode = new CrunchyRollEpisodeData();

        if (crunInstance.CrunOptions.History){
            await crunInstance.CrHistory.UpdateWithEpisode(dlEpisode);
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
                    episode.EpisodeAndLanguages.Langs.Add(Array.Find(Languages.languages, a => a.CrLocale == version.AudioLocale));
                }
            }
        } else{
            // Episode didn't have versions, mark it as such to be logged.
            serieshasversions = false;
            // Ensure there is only one of the same language
            if (episode.EpisodeAndLanguages.Langs.All(a => a.CrLocale != dlEpisode.AudioLocale)){
                // Push to arrays if there are no duplicates of the same language
                episode.EpisodeAndLanguages.Items.Add(dlEpisode);
                episode.EpisodeAndLanguages.Langs.Add(Array.Find(Languages.languages, a => a.CrLocale == dlEpisode.AudioLocale));
            }
        }


        int specialIndex = 1;
        int epIndex = 1;


        var isSpecial = !Regex.IsMatch(episode.EpisodeAndLanguages.Items[0].Episode ?? string.Empty, @"^\d+$"); // Checking if the episode is not a number (i.e., special).
        string newKey;
        if (isSpecial && !string.IsNullOrEmpty(episode.EpisodeAndLanguages.Items[0].Episode)){
            newKey = episode.EpisodeAndLanguages.Items[0].Episode ?? "SP" + (specialIndex + " " + episode.EpisodeAndLanguages.Items[0].Id);
        } else{
            newKey = $"{(isSpecial ? "SP" : 'E')}{(isSpecial ? (specialIndex + " " + episode.EpisodeAndLanguages.Items[0].Id) : episode.EpisodeAndLanguages.Items[0].Episode ?? epIndex + "")}";
        }

        episode.Key = newKey;

        var seasonTitle = episode.EpisodeAndLanguages.Items.FirstOrDefault(a => !Regex.IsMatch(a.SeasonTitle, @"\(\w+ Dub\)")).SeasonTitle
                          ?? Regex.Replace(episode.EpisodeAndLanguages.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd();

        var title = episode.EpisodeAndLanguages.Items[0].Title;
        var seasonNumber = Helpers.ExtractNumberAfterS(episode.EpisodeAndLanguages.Items[0].Identifier) ?? episode.EpisodeAndLanguages.Items[0].SeasonNumber.ToString();

        var languages = episode.EpisodeAndLanguages.Items.Select((a, index) =>
            $"{(a.IsPremiumOnly ? "+ " : "")}{episode.EpisodeAndLanguages.Langs.ElementAtOrDefault(index).Name ?? "Unknown"}").ToArray(); //☆

        Console.WriteLine($"[{episode.Key}] {seasonTitle} - Season {seasonNumber} - {title} [{string.Join(", ", languages)}]");


        if (!serieshasversions){
            Console.WriteLine("Couldn\'t find versions on episode, fell back to old method.");
        }


        // crunchySeriesList.Data = sortedEpisodes;
        //
        //
        // var images = (episode.EpisodeAndLanguages.Items[0].Images?.Thumbnail ?? new List<List<Image>>{ new List<Image>{ new Image{ Source = "/notFound.png" } } });
        // var seconds = (int)Math.Floor(episode.EpisodeAndLanguages.Items[0].DurationMs / 1000.0);
        //
        // var newEpisode = new Episode{
        //     E = episode.Key.StartsWith("E") ? episode.Key.Substring(1) : episode.Key,
        //     Lang = episode.EpisodeAndLanguages.Langs.Select(a => a.Code).ToList(),
        //     Name = episode.EpisodeAndLanguages.Items[0].Title,
        //     Season = Helpers.ExtractNumberAfterS(episode.EpisodeAndLanguages.Items[0].Identifier) ?? episode.EpisodeAndLanguages.Items[0].SeasonNumber.ToString(),
        //     SeriesTitle = Regex.Replace(episode.EpisodeAndLanguages.Items[0].SeriesTitle, @"\(\w+ Dub\)", "").TrimEnd(),
        //     SeasonTitle = Regex.Replace(episode.EpisodeAndLanguages.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd(),
        //     EpisodeNum = episode.EpisodeAndLanguages.Items[0].EpisodeNumber?.ToString() ?? episode.EpisodeAndLanguages.Items[0].Episode ?? "?",
        //     Id = episode.EpisodeAndLanguages.Items[0].SeasonId,
        //     Img = images[images.Count / 2].FirstOrDefault().Source,
        //     Description = episode.EpisodeAndLanguages.Items[0].Description,
        //     Time = $"{seconds / 60}:{seconds % 60:D2}" // Ensures two digits for seconds.
        // };
        //
        // CrunchySeriesList crunchySeriesList = new CrunchySeriesList();

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
            epMeta.SeriesTitle = episodeP.EpisodeAndLanguages.Items.FirstOrDefault(a => !dubPattern.IsMatch(a.SeriesTitle)).SeriesTitle ?? Regex.Replace(episodeP.EpisodeAndLanguages.Items[0].SeriesTitle, @"\(\w+ Dub\)", "").TrimEnd();
            epMeta.SeasonTitle = episodeP.EpisodeAndLanguages.Items.FirstOrDefault(a => !dubPattern.IsMatch(a.SeasonTitle)).SeasonTitle ?? Regex.Replace(episodeP.EpisodeAndLanguages.Items[0].SeasonTitle, @"\(\w+ Dub\)", "").TrimEnd();
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
                Error = false,
                Percent = 0,
                Time = 0,
                DownloadSpeed = 0
            };
            epMeta.AvailableSubs = item.SubtitleLocales;
            epMeta.Description = item.Description; 
            
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

            if (retMeta.Data != null){
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
}