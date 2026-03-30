using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CRD.Downloader.Crunchyroll;
using Newtonsoft.Json;

namespace CRD.Utils.Http;

public class FlareSolverrClient{
    private readonly HttpClient httpClient;

    private FlareSolverrProperties? flareProperties;
    private readonly MitmProxyProperties? mitmProperties;

    private string flaresolverrUrl = "http://localhost:8191";
    private readonly string mitmProxyUrl = "localhost:8080";

    private const string HeaderToken = "$$headers[]";
    private const string PostToken = "$$post";

    public FlareSolverrClient(){
        flareProperties = CrunchyrollManager.Instance.CrunOptions.FlareSolverrProperties;
        mitmProperties = CrunchyrollManager.Instance.CrunOptions.FlareSolverrMitmProperties;

        if (flareProperties != null){
            flaresolverrUrl = $"http{(flareProperties.UseSsl ? "s" : "")}://{(!string.IsNullOrEmpty(flareProperties.Host) ? flareProperties.Host : "localhost")}:{flareProperties.Port}";
        }

        if (mitmProperties != null){
            mitmProxyUrl =
                $"{(!string.IsNullOrWhiteSpace(mitmProperties.Host) ? mitmProperties.Host : "localhost")}:" +
                $"{mitmProperties.Port}";
        }

        httpClient = new HttpClient{ BaseAddress = new Uri(flaresolverrUrl) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
    }


    public Task<(bool IsOk, string ResponseContent, List<Cookie> Cookies, string UserAgent)> SendViaProxySolverAsync(
        HttpRequestMessage request,
        List<Cookie> cookiesToSend){
        return mitmProperties is{ UseMitmProxy: true }
            ? SendViaMitmProxyAsync(request, cookiesToSend)
            : SendViaFlareSolverrAsync(request, cookiesToSend);
    }

    public async Task<(bool IsOk, string ResponseContent, List<Cookie> cookies, string UserAgent)> SendViaFlareSolverrAsync(HttpRequestMessage request, List<Cookie> cookiesToSend){
        var flaresolverrCookies = new List<object>();

        foreach (var cookie in cookiesToSend){
            flaresolverrCookies.Add(new{
                name = cookie.Name,
                value = cookie.Value,
                domain = cookie.Domain,
                path = cookie.Path,
                secure = cookie.Secure,
                httpOnly = cookie.HttpOnly
            });
        }

        var requestData = new{
            cmd = request.Method.Method.ToLower() switch{
                "get" => "request.get",
                "post" => "request.post",
                "patch" => "request.patch",
                _ => "request.get" // Default to GET if the method is unknown
            },
            url = request.RequestUri.ToString(),
            maxTimeout = 60000,
            postData = request.Method == HttpMethod.Post || request.Method == HttpMethod.Patch
                ? await request.Content.ReadAsStringAsync()
                : null,
            cookies = flaresolverrCookies
        };

        // Serialize the request data to JSON
        var json = JsonConvert.SerializeObject(requestData);
        var flareSolverrContent = new StringContent(json, Encoding.UTF8, "application/json");

        // Send the request to FlareSolverr
        var flareSolverrRequest = new HttpRequestMessage(HttpMethod.Post, $"{flaresolverrUrl}/v1"){
            Content = flareSolverrContent
        };

        HttpResponseMessage flareSolverrResponse;
        try{
            flareSolverrResponse = await httpClient.SendAsync(flareSolverrRequest);
        } catch (Exception ex){
            Console.Error.WriteLine($"Error sending request to FlareSolverr: {ex.Message}");
            return (IsOk: false, ResponseContent: $"Error sending request to FlareSolverr: {ex.Message}", [], string.Empty);
        }

        string flareSolverrResponseContent = await flareSolverrResponse.Content.ReadAsStringAsync();

        // Parse the FlareSolverr response
        var flareSolverrResult = JsonConvert.DeserializeObject<FlareSolverrResponse>(flareSolverrResponseContent);

        if (flareSolverrResult != null && flareSolverrResult.Status == "ok"){
            return (IsOk: true, ResponseContent: flareSolverrResult.Solution?.Response ?? string.Empty, flareSolverrResult.Solution?.Cookies ?? [], flareSolverrResult.Solution?.UserAgent ?? string.Empty);
        } else{
            Console.Error.WriteLine($"Flare Solverr Failed \n Response: {flareSolverrResponseContent}");
            return (IsOk: false, ResponseContent: flareSolverrResponseContent, [], string.Empty);
        }
    }

    private Dictionary<string, string> GetHeadersDictionary(HttpRequestMessage request){
        var headers = new Dictionary<string, string>();
        foreach (var header in request.Headers){
            headers[header.Key] = string.Join(", ", header.Value);
        }

        if (request.Content != null){
            foreach (var header in request.Content.Headers){
                headers[header.Key] = string.Join(", ", header.Value);
            }
        }

        return headers;
    }

    private Dictionary<string, string> GetCookiesDictionary(HttpRequestMessage request, Dictionary<string, CookieCollection> cookieStore){
        var cookiesDictionary = new Dictionary<string, string>();
        if (cookieStore.TryGetValue(request.RequestUri.Host, out CookieCollection cookies)){
            foreach (Cookie cookie in cookies){
                cookiesDictionary[cookie.Name] = cookie.Value;
            }
        }

        return cookiesDictionary;
    }

    public async Task<(bool IsOk, string ResponseContent, List<Cookie> Cookies, string UserAgent)> SendViaMitmProxyAsync(
        HttpRequestMessage request,
        List<Cookie> cookiesToSend){
        if (request.RequestUri == null){
            return (false, "RequestUri is null.", [], "");
        }

        var flaresolverrCookies = cookiesToSend.Select(cookie => new{
            name = cookie.Name,
            value = cookie.Value,
            domain = cookie.Domain,
            path = cookie.Path,
            secure = cookie.Secure,
            httpOnly = cookie.HttpOnly
        }).ToList();

        string proxiedUrl = BuildMitmUrl(request);
        string? postData = await BuildPostDataAsync(request);

        var requestData = new{
            cmd = request.Method.Method.ToLowerInvariant() switch{
                "get" => "request.get",
                "post" => "request.post",
                "patch" => "request.patch",
                _ => "request.get"
            },
            url = proxiedUrl,
            maxTimeout = 60000,
            postData,
            cookies = flaresolverrCookies,
            proxy = new{
                url = mitmProxyUrl
            }
        };

        var json = JsonConvert.SerializeObject(requestData);
        var flareSolverrContent = new StringContent(json, Encoding.UTF8, "application/json");

        var flareSolverrRequest = new HttpRequestMessage(HttpMethod.Post, $"{flaresolverrUrl}/v1"){
            Content = flareSolverrContent
        };

        HttpResponseMessage flareSolverrResponse;
        try{
            flareSolverrResponse = await httpClient.SendAsync(flareSolverrRequest);
        } catch (Exception ex){
            Console.Error.WriteLine($"Error sending request to FlareSolverr: {ex.Message}");
            return (false, $"Error sending request to FlareSolverr: {ex.Message}", [], "");
        }

        string flareSolverrResponseContent = await flareSolverrResponse.Content.ReadAsStringAsync();
        var flareSolverrResult = JsonConvert.DeserializeObject<FlareSolverrResponse>(flareSolverrResponseContent);

        if (flareSolverrResult != null && flareSolverrResult.Status == "ok"){
            return (
                true,
                flareSolverrResult.Solution?.Response ?? string.Empty,
                flareSolverrResult.Solution?.Cookies ?? [],
                flareSolverrResult.Solution?.UserAgent ?? string.Empty
            );
        }

        Console.Error.WriteLine($"FlareSolverr MITM failed\nResponse: {flareSolverrResponseContent}");
        return (false, flareSolverrResponseContent, [], "");
    }

    private string BuildMitmUrl(HttpRequestMessage request){
        if (request.RequestUri == null){
            throw new InvalidOperationException("RequestUri is null.");
        }

        var uri = request.RequestUri;
        var parts = new List<string>();

        string existingQuery = uri.Query;
        if (!string.IsNullOrWhiteSpace(existingQuery)){
            parts.Add(existingQuery.TrimStart('?'));
        }

        foreach (var header in GetHeaders(request)){
            // Skip headers that should not be forwarded this way
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)){
                continue;
            }

            string headerValue = $"{header.Key}:{header.Value}";
            parts.Add($"{Uri.EscapeDataString(HeaderToken)}={Uri.EscapeDataString(headerValue)}");
        }

        var builder = new UriBuilder(uri){
            Query = string.Join("&", parts.Where(p => !string.IsNullOrWhiteSpace(p)))
        };

        return builder.Uri.ToString();
    }

    private async Task<string?> BuildPostDataAsync(HttpRequestMessage request){
        if (request.Content == null){
            return null;
        }

        string body = await request.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(body)){
            return null;
        }

        string? mediaType = request.Content.Headers.ContentType?.MediaType;

        // The MITM proxy understands $$post=<base64> for JSON/text POST payloads.
        if (request.Method == HttpMethod.Post &&
            !string.IsNullOrWhiteSpace(mediaType) &&
            mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)){
            string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));
            return $"{PostToken}={base64}";
        }

        // Fallback: send raw post body
        return body;
    }

    private static IEnumerable<KeyValuePair<string, string>> GetHeaders(HttpRequestMessage request){
        foreach (var header in request.Headers){
            yield return new KeyValuePair<string, string>(
                header.Key,
                string.Join(", ", header.Value));
        }

        if (request.Content != null){
            foreach (var header in request.Content.Headers){
                yield return new KeyValuePair<string, string>(
                    header.Key,
                    string.Join(", ", header.Value));
            }
        }
    }
}

public class FlareSolverrResponse{
    public string? Status{ get; set; }
    public FlareSolverrSolution? Solution{ get; set; }
}

public class FlareSolverrSolution{
    [JsonProperty("url")]
    public string? Url{ get; set; }

    [JsonProperty("status")]
    public string? Status{ get; set; }

    [JsonProperty("cookies")]
    public List<Cookie> Cookies{ get; set; } = [];

    [JsonProperty("response")]
    public string? Response{ get; set; } = string.Empty;

    [JsonProperty("userAgent")]
    public string UserAgent{ get; set; } = string.Empty;
}

public class FlareSolverrProperties(){
    public bool UseFlareSolverr{ get; set; }
    public string? Host{ get; set; } = "localhost";
    public int Port{ get; set; }
    public bool UseSsl{ get; set; }
}

public class MitmProxyProperties{
    public bool UseMitmProxy{ get; set; }
    public string? Host{ get; set; } = "localhost";
    public int Port{ get; set; } = 8080;

    public bool UseSsl{ get; set; }
}