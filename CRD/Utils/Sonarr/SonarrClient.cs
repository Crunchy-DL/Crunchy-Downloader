using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CRD.Downloader;
using CRD.Utils.Sonarr.Models;
using CRD.Views;
using Newtonsoft.Json;

namespace CRD.Utils.Sonarr;

public class SonarrClient{
    private string apiUrl;

    private HttpClient httpClient;

    private SonarrProperties properties;

    #region Singelton

    private static SonarrClient? _instance;
    private static readonly object Padlock = new();

    public static SonarrClient Instance{
        get{
            if (_instance == null){
                lock (Padlock){
                    if (_instance == null){
                        _instance = new SonarrClient();
                    }
                }
            }

            return _instance;
        }
    }

    #endregion

    public SonarrClient(){
        httpClient = new HttpClient();
    }

    public void SetApiUrl(){
        if (Crunchyroll.Instance.CrunOptions.SonarrProperties != null) properties = Crunchyroll.Instance.CrunOptions.SonarrProperties;

        if (properties != null){
            apiUrl = $"http{(properties.UseSsl ? "s" : "")}://{properties.Host}:{properties.Port}{(properties.UrlBase ?? "")}/api";
        }
    }

    public async Task<List<SonarrSeries>> GetSeries(){
        var json = await GetJson($"/v3/series{(true ? $"?includeSeasonImages={true}" : "")}");

        List<SonarrSeries> series = [];
        
        try{
            series = JsonConvert.DeserializeObject<List<SonarrSeries>>(json) ?? [];
        } catch (Exception e){
            MainWindow.Instance.ShowError("Sonarr GetSeries error \n" + e);
            Console.WriteLine(e);
        }
        
        return series;
    }
    
    public async Task<List<SonarrEpisode>> GetEpisodes(int seriesId){
        var json = await GetJson($"/v3/episode?seriesId={seriesId}");
        
        List<SonarrEpisode> episodes = [];
        
        try{
            episodes = JsonConvert.DeserializeObject<List<SonarrEpisode>>(json) ?? [];
        } catch (Exception e){
            MainWindow.Instance.ShowError("Sonarr GetSeries error \n" + e);
            Console.WriteLine(e);
        }

        return episodes;
    }


    public async Task<SonarrEpisode> GetEpisode(int episodeId){
        var json = await GetJson($"/v3/episode/id={episodeId}");
        var episode = new SonarrEpisode();
        try{
            episode = JsonConvert.DeserializeObject<SonarrEpisode>(json) ?? new SonarrEpisode();
        } catch (Exception e){
            MainWindow.Instance.ShowError("Sonarr GetSeries error \n" + e);
            Console.WriteLine(e);
        }

        return episode;
    }

    private async Task<string> GetJson(string endpointUrl){
        Debug.WriteLine($"[DEBUG] [SonarrClient.PostJson] Endpoint URL: '{endpointUrl}'");

        var request = CreateRequestMessage($"{apiUrl}{endpointUrl}", HttpMethod.Get);
        HttpResponseMessage response;
        var content = string.Empty;

        try{
            response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            content = await response.Content.ReadAsStringAsync();

        } catch (Exception ex){
            Debug.WriteLine($"[ERROR] [SonarrClient.GetJson] Endpoint URL: '{endpointUrl}', {ex}");
        } 


        if (!string.IsNullOrEmpty(content)) // Convert response to UTF8
            content = Encoding.UTF8.GetString(Encoding.Default.GetBytes(content));

        return content;
    }

    public HttpRequestMessage CreateRequestMessage(string uri, HttpMethod requestMethod, [Optional] NameValueCollection query){
        UriBuilder uriBuilder = new UriBuilder(uri);

        if (query != null){
            uriBuilder.Query = query.ToString();
        }

        var request = new HttpRequestMessage(requestMethod, uriBuilder.ToString());
        
        request.Headers.Add("X-Api-Key", properties.ApiKey);
        
        request.Headers.UserAgent.ParseAdd($"{Assembly.GetExecutingAssembly().GetName().Name.Replace(" ", ".")}.v{Assembly.GetExecutingAssembly().GetName().Version}");


        return request;
    }
    
}



public class SonarrProperties(){
    public string? Host{ get; set; }
    public int Port{ get; set; }
    public string? ApiKey{ get; set; }
    public bool UseSsl{ get; set; }

    public string? UrlBase{ get; set; }
}