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
    
    public HttpClientReq(){
        IWebProxy systemProxy = WebRequest.DefaultWebProxy;

        HttpClientHandler handler = new HttpClientHandler();

        if (CrunchyrollManager.Instance.CrunOptions.ProxyEnabled && !string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.ProxyHost)){
            handler = CreateHandler(true, CrunchyrollManager.Instance.CrunOptions.ProxySocks, CrunchyrollManager.Instance.CrunOptions.ProxyHost, CrunchyrollManager.Instance.CrunOptions.ProxyPort,
                CrunchyrollManager.Instance.CrunOptions.ProxyUsername, CrunchyrollManager.Instance.CrunOptions.ProxyPassword);
            string scheme = CrunchyrollManager.Instance.CrunOptions.ProxySocks ? "socks5" : "http";
            Console.Error.WriteLine($"Proxy is set: {scheme}://{CrunchyrollManager.Instance.CrunOptions.ProxyHost}:{CrunchyrollManager.Instance.CrunOptions.ProxyPort}");
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

        // client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:126.0) Gecko/20100101 Firefox/126.0");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
        // client.DefaultRequestHeaders.UserAgent.ParseAdd("Crunchyroll/1.9.0 Nintendo Switch/18.1.0.0 UE4/4.27");
        // client.DefaultRequestHeaders.UserAgent.ParseAdd("Crunchyroll/3.60.0 Android/9 okhttp/4.12.0");

        client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
        // client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.5");
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

    private HttpClientHandler CreateHandler(bool useProxy, bool useSocks = false, string? proxyHost = null, int proxyPort = 0, string? proxyUsername = "", string? proxyPassword = ""){
        var handler = new HttpClientHandler{
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            UseProxy = useProxy
        };

        if (useProxy && proxyHost != null){
            string scheme = useSocks ? "socks5" : "http";
            handler.Proxy = new WebProxy($"{scheme}://{proxyHost}:{proxyPort}");
            if (!string.IsNullOrEmpty(proxyUsername) && !string.IsNullOrEmpty(proxyPassword)){
                handler.Proxy.Credentials = new NetworkCredential(proxyUsername, proxyPassword);
            }
        }

        return handler;
    }

    public async Task<(bool IsOk, string ResponseContent, string error)> SendHttpRequest(HttpRequestMessage request, bool suppressError = false, Dictionary<string, CookieCollection>? cookieStore = null){
        string content = string.Empty;
        try{
            AttachCookies(request, cookieStore);

            HttpResponseMessage response = await client.SendAsync(request);

            if (ChallengeDetector.IsClearanceRequired(response)){
                Console.Error.WriteLine($" Cloudflare Challenge detected");
            }

            content = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            return (IsOk: true, ResponseContent: content, error: "");
        } catch (Exception e){
            // Console.Error.WriteLine($"Error: {e} \n Response: {(content.Length < 500 ? content : "error to long")}");
            if (!suppressError){
                Console.Error.WriteLine($"Error: {e} \n Response: {(content.Length < 500 ? content : "error to long")}");
            }

            return (IsOk: false, ResponseContent: content, error: e.Message);
        }
    }

    private void AttachCookies(HttpRequestMessage request, Dictionary<string, CookieCollection>? cookieStore){
        if (cookieStore == null){
            return;
        }

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

    public void AddCookie(string domain, Cookie cookie, Dictionary<string, CookieCollection>? cookieStore){
        if (cookieStore == null){
            return;
        }

        if (!cookieStore.ContainsKey(domain)){
            cookieStore[domain] = new CookieCollection();
        }

        var existingCookie = cookieStore[domain].FirstOrDefault(c => c.Name == cookie.Name);

        if (existingCookie != null){
            cookieStore[domain].Remove(existingCookie);
        }

        cookieStore[domain].Add(cookie);
    }


    public static HttpRequestMessage CreateRequestMessage(string uri, HttpMethod requestMethod, bool authHeader, string? accessToken = "", NameValueCollection? query = null){
        if (string.IsNullOrEmpty(uri)){
            Console.Error.WriteLine($" Request URI is empty");
            return new HttpRequestMessage(HttpMethod.Get, "about:blank");
        }

        UriBuilder uriBuilder = new UriBuilder(uri);

        if (query != null){
            uriBuilder.Query = query.ToString();
        }

        var request = new HttpRequestMessage(requestMethod, uriBuilder.ToString());

        if (authHeader){
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }


        return request;
    }


    public HttpClient GetHttpClient(){
        return client;
    }
}

public static class ApiUrls{
    public static readonly string ApiBeta = "https://beta-api.crunchyroll.com";
    public static readonly string ApiN = "https://www.crunchyroll.com";
    public static readonly string Anilist = "https://graphql.anilist.co";

    public static string Auth => (CrunchyrollManager.Instance.CrunOptions.UseCrBetaApi ? ApiBeta : ApiN) + "/auth/v1/token";
    public static string Profile => (CrunchyrollManager.Instance.CrunOptions.UseCrBetaApi ? ApiBeta : ApiN) + "/accounts/v1/me/profile";
    public static string CmsToken => (CrunchyrollManager.Instance.CrunOptions.UseCrBetaApi ? ApiBeta : ApiN) + "/index/v2";
    public static string Search => (CrunchyrollManager.Instance.CrunOptions.UseCrBetaApi ? ApiBeta : ApiN) + "/content/v2/discover/search";
    public static string Browse => (CrunchyrollManager.Instance.CrunOptions.UseCrBetaApi ? ApiBeta : ApiN) + "/content/v2/discover/browse";
    public static string Cms => (CrunchyrollManager.Instance.CrunOptions.UseCrBetaApi ? ApiBeta : ApiN) + "/content/v2/cms";
    public static string Content => (CrunchyrollManager.Instance.CrunOptions.UseCrBetaApi ? ApiBeta : ApiN) + "/content/v2";

    public static string Playback => "https://cr-play-service.prd.crunchyrollsvc.com/v2";
    //https://www.crunchyroll.com/playback/v2
    //https://cr-play-service.prd.crunchyrollsvc.com/v2

    public static string Subscription => (CrunchyrollManager.Instance.CrunOptions.UseCrBetaApi ? ApiBeta : ApiN) + "/subs/v3/subscriptions/";

    public static readonly string BetaBrowse = ApiBeta + "/content/v1/browse";
    public static readonly string BetaCms = ApiBeta + "/cms/v2";
    public static readonly string DRM = ApiBeta + "/drm/v1/auth";

    public static readonly string WidevineLicenceUrl = "https://www.crunchyroll.com/license/v1/license/widevine";
    //https://lic.drmtoday.com/license-proxy-widevine/cenc/
    //https://lic.staging.drmtoday.com/license-proxy-widevine/cenc/

    // public static string authBasicMob = "Basic djV3YnNsdGJueG5oeXk3cDN4ZmI6cFdKWkZMaHVTM0I2NFhPbk81bWVlWXpiTlBtZWsyRVU=";
    public static string authBasicMob = "Basic Ym1icmt4eXgzZDd1NmpzZnlsYTQ6QUlONEQ1VkVfY3Awd1Z6Zk5vUDBZcUhVcllGcDloU2c=";

    public static readonly string MobileUserAgent = "Crunchyroll/3.81.6 Android/16";
    public static readonly string FirefoxUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:137.0) Gecko/20100101 Firefox/137.0";
}