using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CRD.Downloader.Crunchyroll;
using Newtonsoft.Json;

namespace CRD.Utils;

public class FlareSolverrClient{
    private readonly HttpClient _httpClient;

    private FlareSolverrProperties properties;

    private string flaresolverrUrl = "http://localhost:8191";

    public FlareSolverrClient(){
        if (CrunchyrollManager.Instance.CrunOptions.FlareSolverrProperties != null) properties = CrunchyrollManager.Instance.CrunOptions.FlareSolverrProperties;

        if (properties != null){
            flaresolverrUrl = $"http{(properties.UseSsl ? "s" : "")}://{(!string.IsNullOrEmpty(properties.Host) ? properties.Host : "localhost")}:{properties.Port}";
        }

        _httpClient = new HttpClient{ BaseAddress = new Uri(flaresolverrUrl) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");
    }


    public async Task<(bool IsOk, string ResponseContent, List<Cookie> cookies)> SendViaFlareSolverrAsync(HttpRequestMessage request,List<Cookie> cookiesToSend){
        
        var flaresolverrCookies = new List<object>();

        foreach (var cookie in cookiesToSend)
        {
            flaresolverrCookies.Add(new
            {
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
            flareSolverrResponse = await _httpClient.SendAsync(flareSolverrRequest);
        } catch (Exception ex){
            Console.Error.WriteLine($"Error sending request to FlareSolverr: {ex.Message}");
            return (IsOk: false, ResponseContent: $"Error sending request to FlareSolverr: {ex.Message}", []);
        }

        string flareSolverrResponseContent = await flareSolverrResponse.Content.ReadAsStringAsync();

        // Parse the FlareSolverr response
        var flareSolverrResult = JsonConvert.DeserializeObject<FlareSolverrResponse>(flareSolverrResponseContent);

        if (flareSolverrResult != null && flareSolverrResult.Status == "ok"){
            return (IsOk: true, ResponseContent: flareSolverrResult.Solution.Response, flareSolverrResult.Solution.cookies);
        } else{
            Console.Error.WriteLine($"Flare Solverr Failed \n Response: {flareSolverrResponseContent}");
            return (IsOk: false, ResponseContent: flareSolverrResponseContent, []);
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
}

public class FlareSolverrResponse{
    public string Status{ get; set; }
    public FlareSolverrSolution Solution{ get; set; }
}

public class FlareSolverrSolution{
    public string Url{ get; set; }
    public string Status{ get; set; }
    public List<Cookie> cookies{ get; set; }
    public string Response{ get; set; }
}

public class FlareSolverrProperties(){
    public bool UseFlareSolverr{ get; set; }
    public string? Host{ get; set; }
    public int Port{ get; set; }
    public bool UseSsl{ get; set; }
}