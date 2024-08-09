using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll.Music;

namespace CRD.Downloader.Crunchyroll;

public class CrMusic{
    private readonly CrunchyrollManager crunInstance = CrunchyrollManager.Instance;

    public async Task<CrunchyMusicVideo?> ParseMusicVideoByIdAsync(string id, string crLocale, bool forcedLang = false){
        return await ParseMediaByIdAsync(id, crLocale, forcedLang, "music/music_videos");
    }

    public async Task<CrunchyMusicVideo?> ParseConcertByIdAsync(string id, string crLocale, bool forcedLang = false){
        return await ParseMediaByIdAsync(id, crLocale, forcedLang, "music/concerts");
    }

    public async Task<CrunchyMusicVideoList?> ParseArtistMusicVideosByIdAsync(string id, string crLocale, bool forcedLang = false){
        var musicVideosTask = FetchMediaListAsync($"{Api.Content}/music/artists/{id}/music_videos", crLocale, forcedLang);
        var concertsTask = FetchMediaListAsync($"{Api.Content}/music/artists/{id}/concerts", crLocale, forcedLang);

        await Task.WhenAll(musicVideosTask, concertsTask);

        var musicVideos = await musicVideosTask;
        var concerts = await concertsTask;
        
        musicVideos.Total += concerts.Total;
        musicVideos.Data ??= new List<CrunchyMusicVideo>();

        if (concerts.Data != null){
            musicVideos.Data.AddRange(concerts.Data);
        }

        return musicVideos;
    }

    private async Task<CrunchyMusicVideo?> ParseMediaByIdAsync(string id, string crLocale, bool forcedLang, string endpoint){
        var mediaList = await FetchMediaListAsync($"{Api.Content}/{endpoint}/{id}", crLocale, forcedLang);

        switch (mediaList.Total){
            case < 1:
                return null;
            case 1 when mediaList.Data != null:
                return mediaList.Data.First();
            default:
                Console.Error.WriteLine($"Multiple items returned for endpoint {endpoint} with ID {id}");
                return mediaList.Data?.First();
        }
    }

    private async Task<CrunchyMusicVideoList> FetchMediaListAsync(string url, string crLocale, bool forcedLang){
        var query = CreateQueryParameters(crLocale, forcedLang);
        var request = HttpClientReq.CreateRequestMessage(url, HttpMethod.Get, true, true, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine($"Request to {url} failed");
            return new CrunchyMusicVideoList();
        }

        return Helpers.Deserialize<CrunchyMusicVideoList>(response.ResponseContent,  crunInstance.SettingsJsonSerializerSettings);
    }

    private NameValueCollection CreateQueryParameters(string crLocale, bool forcedLang){
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["preferred_audio_language"] = "ja-JP";

        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;

            if (forcedLang){
                query["force_locale"] = crLocale;
            }
        }

        return query;
    }


    public CrunchyEpMeta EpisodeMeta(CrunchyMusicVideo episodeP){
        var images = (episodeP.Images?.Thumbnail ?? new List<Image>{ new Image{ Source = "/notFound.png" } });

        var epMeta = new CrunchyEpMeta();
        epMeta.Data = new List<CrunchyEpMetaData>{ new(){ MediaId = episodeP.Id, Versions = null } };
        epMeta.SeriesTitle = "Music";
        epMeta.SeasonTitle = episodeP.DisplayArtistName;
        epMeta.EpisodeNumber = episodeP.SequenceNumber + "";
        epMeta.EpisodeTitle = episodeP.Title;
        epMeta.SeasonId = "";
        epMeta.Season = "";
        epMeta.ShowId = "";
        epMeta.AbsolutEpisodeNumberE = "";
        epMeta.Image = images[images.Count / 2].Source;
        epMeta.DownloadProgress = new DownloadProgress(){
            IsDownloading = false,
            Done = false,
            Error = false,
            Percent = 0,
            Time = 0,
            DownloadSpeed = 0
        };
        epMeta.AvailableSubs = new List<string>();
        epMeta.Description = episodeP.Description;
        epMeta.Music = true;

        return epMeta;
    }
}