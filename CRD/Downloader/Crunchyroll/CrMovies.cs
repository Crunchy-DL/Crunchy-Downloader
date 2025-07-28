using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using CRD.Utils;
using CRD.Utils.Structs;

namespace CRD.Downloader.Crunchyroll;

public class CrMovies{
    private readonly CrunchyrollManager crunInstance = CrunchyrollManager.Instance;

    public async Task<CrunchyMovie?> ParseMovieById(string id, string crLocale, bool forcedLang = false){
        NameValueCollection query = HttpUtility.ParseQueryString(new UriBuilder().Query);

        query["preferred_audio_language"] = "ja-JP";
        if (!string.IsNullOrEmpty(crLocale)){
            query["locale"] = crLocale;
            if (forcedLang){
                query["force_locale"] = crLocale;
            }
        }


        var request = HttpClientReq.CreateRequestMessage($"{ApiUrls.Cms}/objects/{id}", HttpMethod.Get, true, true, query);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine("Movie Request Failed");
            return null;
        }

        CrunchyMovieList movie = Helpers.Deserialize<CrunchyMovieList>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings) ?? new CrunchyMovieList();

        if (movie.Total < 1){
            return null;
        }

        if (movie is{ Total: 1, Data: not null }){
            var movieRes = movie.Data.First();
            return movieRes.type != "movie" ? null : movieRes;
        }

        Console.Error.WriteLine("Multiple movie returned with one ID?");
        if (movie.Data != null){
            var movieRes = movie.Data.First();
            return movieRes.type != "movie" ? null : movieRes;
        }

        return null;
    }


    public CrunchyEpMeta? EpisodeMeta(CrunchyMovie episodeP, List<string> dubLang){
        if (!string.IsNullOrEmpty(episodeP.AudioLocale) && !dubLang.Contains(episodeP.AudioLocale)){
            Console.Error.WriteLine("Movie not available in the selected dub lang");
            return null;
        }

        var images = (episodeP.Images?.Thumbnail ??[new List<Image>(){ new(){ Source = "/notFound.jpg" } }]);

        var epMeta = new CrunchyEpMeta();
        epMeta.Data = new List<CrunchyEpMetaData>{ new(){ MediaId = episodeP.Id, Versions = null, IsSubbed = episodeP.IsSubbed, IsDubbed = episodeP.IsDubbed } };
        epMeta.SeriesTitle = "Movie";
        epMeta.SeasonTitle = "";
        epMeta.EpisodeNumber = "";
        epMeta.EpisodeTitle = episodeP.Title;
        epMeta.SeasonId = "";
        epMeta.Season = "";
        epMeta.SeriesId = "";
        epMeta.AbsolutEpisodeNumberE = "";
        epMeta.Image = images[images.Count / 2].FirstOrDefault()?.Source;
        epMeta.ImageBig = images[images.Count / 2].LastOrDefault()?.Source;
        epMeta.DownloadProgress = new DownloadProgress(){
            IsDownloading = false,
            Done = false,
            Error = false,
            Percent = 0,
            Time = 0,
            DownloadSpeed = 0
        };
        epMeta.AvailableSubs = [];
        epMeta.Description = episodeP.Description;
        epMeta.Hslang = CrunchyrollManager.Instance.CrunOptions.Hslang;

        return epMeta;
    }
}