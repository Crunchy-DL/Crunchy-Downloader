using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CRD.Downloader;

namespace CRD.Utils;

public class HttpClientReq{
    #region Singelton

    private static HttpClientReq? instance;
    private static readonly object padlock = new object();

    public static HttpClientReq Instance{
        get{
            if (instance == null){
                lock (padlock){
                    if (instance == null){
                        instance = new HttpClientReq();
                    }
                }
            }

            return instance;
        }
    }

    #endregion


    private HttpClient client;
    private HttpClientHandler handler;

    public HttpClientReq(){
        // Initialize the HttpClientHandler
        handler = new HttpClientHandler();
        handler.CookieContainer = new CookieContainer();
        handler.UseCookies = true;

        // Initialize the HttpClient with the handler
        client = new HttpClient(handler);

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:124.0) Gecko/20100101 Firefox/124.0");

        // // Set Accept headers
        // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
        // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif"));
        // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
        // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/apng"));
        // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
        // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/signed-exchange", 0.7));
        //
        // // Set Accept-Language
        // client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
        // client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.9));
        //
        // // Set Cache-Control and Pragma for no caching
        // client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue{ NoCache = true };
        // client.DefaultRequestHeaders.Pragma.Add(new NameValueHeaderValue("no-cache"));
        //
        // // Set other headers
        // client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Google Chrome\";v=\"123\", \"Not:A-Brand\";v=\"8\", \"Chromium\";v=\"123\"");
        // client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        // client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
        // client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
        // client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
        // client.DefaultRequestHeaders.Add("sec-fetch-site", "none");
        // client.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
        // client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
    }

    public void SetETPCookie(string refresh_token){
        var cookie = new Cookie("etp_rt", refresh_token){
            Domain = "crunchyroll.com",
            Path = "/",
        };

        handler.CookieContainer.Add(cookie);
    }

    public async Task<(bool IsOk, string ResponseContent)> SendHttpRequest(HttpRequestMessage request){
        try{
            HttpResponseMessage response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();
            return (IsOk: true, ResponseContent: content);
        } catch (Exception e){
            Console.WriteLine(e);
            return (IsOk: false, ResponseContent: String.Empty);
        }
    }

    public static HttpRequestMessage CreateRequestMessage(string uri, HttpMethod requestMethod, bool authHeader, bool disableDrmHeader, NameValueCollection? query){
        UriBuilder uriBuilder = new UriBuilder(uri);

        if (query != null){
            uriBuilder.Query = query.ToString();
        }

        var request = new HttpRequestMessage(requestMethod, uriBuilder.ToString());

        if (authHeader){
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Crunchyroll.Instance.Token?.access_token);
        }

        if (disableDrmHeader){
            request.Headers.Add("X-Cr-Disable-Drm", "true");
        }


        return request;
    }

    public HttpClient GetHttpClient(){
        return client;
    }

}

public static class Api{
    public static readonly string ApiBeta = "https://beta-api.crunchyroll.com";
    public static readonly string ApiN = "https://crunchyroll.com";

    public static readonly string BetaAuth = ApiBeta + "/auth/v1/token";
    public static readonly string BetaProfile = ApiBeta + "/accounts/v1/me/profile";
    public static readonly string BetaCmsToken = ApiBeta + "/index/v2";
    public static readonly string Search = ApiBeta + "/content/v2/discover/search";
    public static readonly string Cms = ApiBeta + "/content/v2/cms";
    public static readonly string BetaBrowse = ApiBeta + "/content/v1/browse";
    public static readonly string BetaCms = ApiBeta + "/cms/v2";


    public static readonly string CmsN = ApiN + "/content/v2/cms";


    public static readonly string authBasic = "bm9haWhkZXZtXzZpeWcwYThsMHE6";
    public static readonly string authBasicMob = "bm12anNoZmtueW14eGtnN2ZiaDk6WllJVnJCV1VQYmNYRHRiRDIyVlNMYTZiNFdRb3Mzelg=";
    public static readonly string authBasicSwitch = "dC1rZGdwMmg4YzNqdWI4Zm4wZnE6eWZMRGZNZnJZdktYaDRKWFMxTEVJMmNDcXUxdjVXYW4=";
}