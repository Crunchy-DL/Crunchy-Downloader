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
        
        // client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:126.0) Gecko/20100101 Firefox/126.0");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Crunchyroll/1.8.0 Nintendo Switch/12.3.12.0 UE4/4.27");
        
    }

    public void SetETPCookie(string refresh_token){
        var cookie = new Cookie("etp_rt", refresh_token){
            Domain = "crunchyroll.com",
            Path = "/",
        };

        handler.CookieContainer.Add(cookie);
    }

    public async Task<(bool IsOk, string ResponseContent)> SendHttpRequest(HttpRequestMessage request){

        string content = string.Empty;
        try{
            HttpResponseMessage response = await client.SendAsync(request);

            content = await response.Content.ReadAsStringAsync();
            
            response.EnsureSuccessStatusCode();
            
            return (IsOk: true, ResponseContent: content);
        } catch (Exception e){
            Console.Error.WriteLine($"Error: {e} \n Response: {(content.Length < 500 ? content : "error to long")}");
            return (IsOk: false, ResponseContent: content);
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
            request.Headers.Add("x-cr-stream-limits", "false");
        }


        return request;
    }

    public static async Task DeAuthVideo(string currentMediaId, string token){
        var deauthVideoToken = HttpClientReq.CreateRequestMessage($"https://cr-play-service.prd.crunchyrollsvc.com/v1/token/{currentMediaId}/{token}/inactive", HttpMethod.Patch, true, false, null);
        var deauthVideoTokenResponse = await HttpClientReq.Instance.SendHttpRequest(deauthVideoToken);
    }

    public HttpClient GetHttpClient(){
        return client;
    }

}

public static class Api{
    public static readonly string ApiBeta = "https://beta-api.crunchyroll.com";
    public static readonly string ApiN = "https://www.crunchyroll.com";

    public static readonly string BetaAuth = ApiBeta + "/auth/v1/token";
    public static readonly string BetaProfile = ApiBeta + "/accounts/v1/me/profile";
    public static readonly string BetaCmsToken = ApiBeta + "/index/v2";
    public static readonly string Search = ApiBeta + "/content/v2/discover/search";
    public static readonly string Cms = ApiBeta + "/content/v2/cms";
    public static readonly string BetaBrowse = ApiBeta + "/content/v1/browse";
    public static readonly string BetaCms = ApiBeta + "/cms/v2";
    public static readonly string DRM = ApiBeta + "/drm/v1/auth";

    public static readonly string Subscription = ApiBeta + "/subs/v3/subscriptions/";
    public static readonly string CmsN = ApiN + "/content/v2/cms";


    public static readonly string authBasic = "bm9haWhkZXZtXzZpeWcwYThsMHE6";
    public static readonly string authBasicMob = "bm12anNoZmtueW14eGtnN2ZiaDk6WllJVnJCV1VQYmNYRHRiRDIyVlNMYTZiNFdRb3Mzelg=";
    public static readonly string authBasicSwitch = "dC1rZGdwMmg4YzNqdWI4Zm4wZnE6eWZMRGZNZnJZdktYaDRKWFMxTEVJMmNDcXUxdjVXYW4=";
}