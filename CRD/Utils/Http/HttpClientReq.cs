using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CRD.Downloader.Crunchyroll;

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
    private Dictionary<string, CookieCollection> cookieStore;

    private HttpClientHandler handler;

    public HttpClientReq(){
        cookieStore = new Dictionary<string, CookieCollection>();

        IWebProxy systemProxy = WebRequest.DefaultWebProxy;

        HttpClientHandler handler = new HttpClientHandler();

        if (CrunchyrollManager.Instance.CrunOptions.ProxyEnabled && !string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.ProxyHost)){
            handler = CreateHandler(true, CrunchyrollManager.Instance.CrunOptions.ProxyHost, CrunchyrollManager.Instance.CrunOptions.ProxyPort);
            Console.Error.WriteLine($"Proxy is set: http://{CrunchyrollManager.Instance.CrunOptions.ProxyHost}:{CrunchyrollManager.Instance.CrunOptions.ProxyPort}");
            client = new HttpClient(handler);
        } else if (systemProxy != null){
            Uri testUri = new Uri("https://icanhazip.com");
            Uri? proxyUri = systemProxy.GetProxy(testUri);

            if (proxyUri != null && proxyUri != testUri){
                if (proxyUri is{ Host: "127.0.0.1", Port: 7890 }){
                    Console.Error.WriteLine($"Proxy is set: {proxyUri}");
                    handler = CreateHandler(true);
                } else{
                    Console.Error.WriteLine("No proxy will be used.");
                    handler = CreateHandler(false);
                }

                client = new HttpClient(handler);
            } else{
                Console.Error.WriteLine("No proxy is being used.");
                client = new HttpClient(CreateHttpClientHandler());
            }
        } else{
            Console.Error.WriteLine("No proxy is being used.");
            client = new HttpClient(CreateHttpClientHandler());
        }
        
        client.Timeout = TimeSpan.FromSeconds(100);

        // client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:126.0) Gecko/20100101 Firefox/126.0");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
        // client.DefaultRequestHeaders.UserAgent.ParseAdd("Crunchyroll/1.9.0 Nintendo Switch/18.1.0.0 UE4/4.27");
        // client.DefaultRequestHeaders.UserAgent.ParseAdd("Crunchyroll/3.60.0 Android/9 okhttp/4.12.0");

        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.5");
        client.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");
        
    }

    private HttpMessageHandler CreateHttpClientHandler(){
        return new SocketsHttpHandler(){
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectCallback = async (context, cancellationToken) => {
                // Resolve IPv4 addresses only
                var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken).ConfigureAwait(false);

                // Create an IPv4 socket
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = true;

                try{
                    await socket.ConnectAsync(entry.AddressList, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                } catch{
                    socket.Dispose();
                    throw;
                }
            }
        };
    }

    private HttpClientHandler CreateHandler(bool useProxy, string? proxyHost = null, int proxyPort = 0){
        var handler = new HttpClientHandler{
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            UseProxy = useProxy
        };

        if (useProxy && proxyHost != null){
            handler.Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}");
        }

        return handler;
    }


    public void SetETPCookie(string refreshToken){
        // var cookie = new Cookie("etp_rt", refreshToken){
        //     Domain = "crunchyroll.com",
        //     Path = "/",
        // };
        //
        // var cookie2 = new Cookie("c_locale", "en-US"){
        //     Domain = "crunchyroll.com",
        //     Path = "/",
        // };
        //
        // handler.CookieContainer.Add(cookie);
        // handler.CookieContainer.Add(cookie2);

        AddCookie(".crunchyroll.com", new Cookie("etp_rt", refreshToken));
        AddCookie(".crunchyroll.com", new Cookie("c_locale", "en-US"));
    }

    private void AddCookie(string domain, Cookie cookie){
        if (!cookieStore.ContainsKey(domain)){
            cookieStore[domain] = new CookieCollection();
        }

        var existingCookie = cookieStore[domain].FirstOrDefault(c => c.Name == cookie.Name);

        if (existingCookie != null){
            cookieStore[domain].Remove(existingCookie);
        }

        cookieStore[domain].Add(cookie);
    }

    public async Task<(bool IsOk, string ResponseContent)> SendHttpRequest(HttpRequestMessage request, bool suppressError = false){
        string content = string.Empty;
        try{
            AttachCookies(request);

            HttpResponseMessage response = await client.SendAsync(request);

            if (ChallengeDetector.IsClearanceRequired(response)){
                Console.Error.WriteLine($" Cloudflare Challenge detected");
            }

            content = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            return (IsOk: true, ResponseContent: content);
        } catch (Exception e){
            // Console.Error.WriteLine($"Error: {e} \n Response: {(content.Length < 500 ? content : "error to long")}");
            if (!suppressError){
                Console.Error.WriteLine($"Error: {e} \n Response: {(content.Length < 500 ? content : "error to long")}");
            }

            return (IsOk: false, ResponseContent: content);
        }
    }

    private void AttachCookies(HttpRequestMessage request){
        var cookieHeader = new StringBuilder();

        if (request.Headers.TryGetValues("Cookie", out var existingCookies)){
            cookieHeader.Append(string.Join("; ", existingCookies));
        }

        foreach (var cookie in cookieStore.SelectMany(keyValuePair => keyValuePair.Value)){
            string cookieString = $"{cookie.Name}={cookie.Value}";

            if (!cookieHeader.ToString().Contains(cookieString)){
                if (cookieHeader.Length > 0){
                    cookieHeader.Append("; ");
                }

                cookieHeader.Append(cookieString);
            }
        }

        if (cookieHeader.Length > 0){
            request.Headers.Remove("Cookie");
            request.Headers.Add("Cookie", cookieHeader.ToString());
        }
    }


    public static HttpRequestMessage CreateRequestMessage(string uri, HttpMethod requestMethod, bool authHeader, bool disableDrmHeader, NameValueCollection? query){
        UriBuilder uriBuilder = new UriBuilder(uri);

        if (query != null){
            uriBuilder.Query = query.ToString();
        }

        var request = new HttpRequestMessage(requestMethod, uriBuilder.ToString());

        if (authHeader){
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CrunchyrollManager.Instance.Token?.access_token);
        }

        if (disableDrmHeader){
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

public static class ApiUrls{
    public static readonly string ApiBeta = "https://beta-api.crunchyroll.com";
    public static readonly string ApiN = "https://www.crunchyroll.com";
    public static readonly string Anilist = "https://graphql.anilist.co";

    public static readonly string Auth = ApiN + "/auth/v1/token";
    public static readonly string BetaAuth = ApiBeta + "/auth/v1/token";
    public static readonly string BetaProfile = ApiBeta + "/accounts/v1/me/profile";
    public static readonly string BetaCmsToken = ApiBeta + "/index/v2";
    public static readonly string Search = ApiBeta + "/content/v2/discover/search";
    public static readonly string Browse = ApiBeta + "/content/v2/discover/browse";
    public static readonly string Cms = ApiBeta + "/content/v2/cms";
    public static readonly string Content = ApiBeta + "/content/v2";
    public static readonly string BetaBrowse = ApiBeta + "/content/v1/browse";
    public static readonly string BetaCms = ApiBeta + "/cms/v2";
    public static readonly string DRM = ApiBeta + "/drm/v1/auth";

    public static readonly string Subscription = ApiBeta + "/subs/v3/subscriptions/";
    public static readonly string CmsN = ApiN + "/content/v2/cms";

    public static readonly string authBasic = "Basic bm9haWhkZXZtXzZpeWcwYThsMHE6";

    public static readonly string authBasicMob = "Basic dXU4aG0wb2g4dHFpOWV0eXl2aGo6SDA2VnVjRnZUaDJ1dEYxM0FBS3lLNE85UTRhX3BlX1o=";
    public static readonly string authBasicSwitch = "Basic dC1rZGdwMmg4YzNqdWI4Zm4wZnE6eWZMRGZNZnJZdktYaDRKWFMxTEVJMmNDcXUxdjVXYW4=";

    public static readonly string ChromeUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
}