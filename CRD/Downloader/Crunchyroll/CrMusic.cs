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

    public async Task<CrunchyMusicVideoList?> ParseFeaturedMusicVideoByIdAsync(string seriesId, string crLocale, bool forcedLang = false, bool updateHistory = false){
        var musicVideos = await FetchMediaListAsync($"{ApiUrls.Content}/music/featured/{seriesId}", crLocale, forcedLang);

        if (musicVideos.Data is{ Count: > 0 } && updateHistory && crunInstance.CrunOptions.HistoryIncludeCrArtists){
            await crunInstance.History.UpdateWithMusicEpisodeList(musicVideos.Data);
        }

        return musicVideos;
    }
    
    public async Task<CrunchyMusicVideo?> ParseMusicVideoByIdAsync(string id, string crLocale, bool forcedLang = false, bool updateHistory = false){
        var musicVideo = await ParseMediaByIdAsync(id, crLocale, forcedLang, "music/music_videos");

        if (musicVideo != null && updateHistory && crunInstance.CrunOptions.HistoryIncludeCrArtists){
            await crunInstance.History.UpdateWithMusicEpisodeList([musicVideo]);
        }

        return musicVideo;
    }

    public async Task<CrunchyMusicVideo?> ParseConcertByIdAsync(string id, string crLocale, bool forcedLang = false, bool updateHistory = false){
        var concert = await ParseMediaByIdAsync(id, crLocale, forcedLang, "music/concerts");

        if (concert != null){
            concert.EpisodeType = EpisodeType.Concert;
            if (updateHistory && crunInstance.CrunOptions.HistoryIncludeCrArtists){
                await crunInstance.History.UpdateWithMusicEpisodeList([concert]);
            }
        }

        return concert;
    }

    public async Task<CrunchyMusicVideoList?> ParseArtistMusicVideosByIdAsync(string artistId, string crLocale, bool forcedLang = false, bool updateHistory = false){
        var musicVideos = await FetchMediaListAsync($"{ApiUrls.Content}/music/artists/{artistId}/music_videos", crLocale, forcedLang);

        if (updateHistory && crunInstance.CrunOptions.HistoryIncludeCrArtists){
            await crunInstance.History.UpdateWithMusicEpisodeList(musicVideos.Data);
        }

        return musicVideos;
    }

    public async Task<CrunchyMusicVideoList?> ParseArtistConcertVideosByIdAsync(string artistId, string crLocale, bool forcedLang = false, bool updateHistory = false){
        var concerts = await FetchMediaListAsync($"{ApiUrls.Content}/music/artists/{artistId}/concerts", crLocale, forcedLang);
        
        if (concerts.Data.Count > 0){
            foreach (var crunchyConcertVideo in concerts.Data){
                crunchyConcertVideo.EpisodeType = EpisodeType.Concert;
            }
        }

        if (updateHistory && crunInstance.CrunOptions.HistoryIncludeCrArtists){
            await crunInstance.History.UpdateWithMusicEpisodeList(concerts.Data);
        }

        return concerts;
    }


    public async Task<CrunchyMusicVideoList?> ParseArtistVideosByIdAsync(string? artistId, string crLocale, bool forcedLang = false, bool updateHistory = false){
        if (string.IsNullOrEmpty(artistId)){
            return new CrunchyMusicVideoList();
        }
        
        var musicVideosTask = FetchMediaListAsync($"{ApiUrls.Content}/music/artists/{artistId}/music_videos", crLocale, forcedLang);
        var concertsTask = FetchMediaListAsync($"{ApiUrls.Content}/music/artists/{artistId}/concerts", crLocale, forcedLang);

        await Task.WhenAll(musicVideosTask, concertsTask);

        var musicVideos = await musicVideosTask;
        var concerts = await concertsTask;

        musicVideos.Total += concerts.Total;

        if (concerts.Data.Count > 0){
            foreach (var crunchyConcertVideo in concerts.Data){
                crunchyConcertVideo.EpisodeType = EpisodeType.Concert;
            }

            musicVideos.Data.AddRange(concerts.Data);
        }

        if (updateHistory && crunInstance.CrunOptions.HistoryIncludeCrArtists){
            await crunInstance.History.UpdateWithMusicEpisodeList(musicVideos.Data);
        }

        return musicVideos;
    }

    public async Task<CrArtist> ParseArtistByIdAsync(string id, string crLocale, bool forcedLang = false){
        var query = CreateQueryParameters(crLocale, forcedLang);
        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Content}/music/artists/{id}", HttpMethod.Get, true, true, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine($"Request to {ApiUrls.Content}/music/artists/{id} failed");
            return new CrArtist();
        }

        var artistList = Helpers.Deserialize<CrunchyArtistList>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings) ?? new CrunchyArtistList();

        return artistList.Data.FirstOrDefault() ?? new CrArtist();
    }

    private async Task<CrunchyMusicVideo?> ParseMediaByIdAsync(string id, string crLocale, bool forcedLang, string endpoint){
        var mediaList = await FetchMediaListAsync($"{ApiUrls.Content}/{endpoint}/{id}", crLocale, forcedLang);

        switch (mediaList.Total){
            case < 1:
                return null;
            case 1 when mediaList.Data.Count > 0:
                return mediaList.Data.First();
            default:
                Console.Error.WriteLine($"Multiple items returned for endpoint {endpoint} with ID {id}");
                return mediaList.Data.First();
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

        return Helpers.Deserialize<CrunchyMusicVideoList>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings) ?? new CrunchyMusicVideoList();
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
        var images = (episodeP.Images?.Thumbnail ??[new Image{ Source = "/notFound.jpg" }]);

        
        var epMeta = new CrunchyEpMeta();
        epMeta.Data = new List<CrunchyEpMetaData>{ new(){ MediaId = episodeP.Id, Versions = null } };
        epMeta.SeriesTitle = episodeP.GetSeriesTitle();
        epMeta.SeasonTitle = episodeP.GetSeasonTitle();
        epMeta.EpisodeNumber = episodeP.SequenceNumber + "";
        epMeta.EpisodeTitle = episodeP.GetEpisodeTitle();
        epMeta.SeasonId = episodeP.GetSeasonId();
        epMeta.Season = "";
        epMeta.SeriesId = episodeP.GetSeriesId();
        epMeta.AbsolutEpisodeNumberE = "";
        epMeta.Image = images.FirstOrDefault()?.Source ?? string.Empty;
        epMeta.ImageBig = images.FirstOrDefault()?.Source ?? string.Empty;
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
        epMeta.Hslang = CrunchyrollManager.Instance.CrunOptions.Hslang;

        return epMeta;
    }
}