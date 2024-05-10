using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Views;
using Newtonsoft.Json;
using ReactiveUI;

namespace CRD.Downloader;

public class History(Crunchyroll crunInstance){
    public async Task UpdateSeries(string seriesId, string? seasonId){
        await crunInstance.CrAuth.RefreshToken(true);

        CrSeriesSearch? parsedSeries = await crunInstance.CrSeries.ParseSeriesById(seriesId, "ja");

        if (parsedSeries == null){
            Console.WriteLine("Parse Data Invalid");
            return;
        }

        var result = crunInstance.CrSeries.ParseSeriesResult(parsedSeries);
        Dictionary<string, EpisodeAndLanguage> episodes = new Dictionary<string, EpisodeAndLanguage>();

        foreach (int season in result.Keys){
            foreach (var key in result[season].Keys){
                var s = result[season][key];
                if (!string.IsNullOrEmpty(seasonId) && s.Id != seasonId) continue;
                var seasonData = await crunInstance.CrSeries.GetSeasonDataById(s);
                UpdateWithSeasonData(seasonData);
            }
        }
    }

    private void UpdateHistoryFile(){
        CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, crunInstance.HistoryList);
    }

    public void SetAsDownloaded(string? seriesId, string? seasonId, string episodeId){
        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);

        if (historySeries != null){
            var historySeason = historySeries.Seasons.Find(s => s.SeasonId == seasonId);

            if (historySeason != null){
                var historyEpisode = historySeason.EpisodesList.Find(e => e.EpisodeId == episodeId);

                if (historyEpisode != null){
                    historyEpisode.WasDownloaded = true;
                    historySeason.UpdateDownloaded();
                    return;
                }
            }
        }

        MessageBus.Current.SendMessage(new ToastMessage($"Couldn't update download History", ToastType.Warning, 1));
    }


    public async void UpdateWithEpisode(CrunchyEpisode episodeParam){
        var episode = episodeParam;

        if (episode.Versions != null){
            var version = episode.Versions.Find(a => a.Original);
            if (version.AudioLocale != episode.AudioLocale){
                var episodeById = await crunInstance.CrEpisode.ParseEpisodeById(version.Guid, "");
                if (episodeById?.Data != null){
                    if (episodeById.Value.Total != 1){
                        MessageBus.Current.SendMessage(new ToastMessage($"Couldn't update download History", ToastType.Warning, 1));
                        return;
                    }

                    episode = episodeById.Value.Data.First();
                }
            }
        }


        var seriesId = episode.SeriesId;
        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);
        if (historySeries != null){
            var historySeason = historySeries.Seasons.Find(s => s.SeasonId == episode.SeasonId);

            if (historySeason != null){
                if (historySeason.EpisodesList.All(e => e.EpisodeId != episode.Id)){
                    var newHistoryEpisode = new HistoryEpisode{
                        EpisodeTitle = episode.Title,
                        EpisodeId = episode.Id,
                        Episode = episode.Episode,
                    };

                    historySeason.EpisodesList.Add(newHistoryEpisode);

                    historySeason.EpisodesList.Sort(new NumericStringPropertyComparer());
                }
            } else{
                var newSeason = NewHistorySeason(episode);

                historySeries.Seasons.Add(newSeason);

                historySeries.Seasons = historySeries.Seasons.OrderBy(s => s.SeasonNum).ToList();
            }
            historySeries.UpdateNewEpisodes();
        } else{
            var newHistorySeries = new HistorySeries{
                SeriesTitle = episode.SeriesTitle,
                SeriesId = episode.SeriesId,
                Seasons =[],
            };
            crunInstance.HistoryList.Add(newHistorySeries);
            var newSeason = NewHistorySeason(episode);

            var series = await crunInstance.CrSeries.SeriesById(seriesId);
            if (series?.Data != null){
                newHistorySeries.SeriesDescription = series.Data.First().Description;
                newHistorySeries.ThumbnailImageUrl = GetSeriesThumbnail(series);
            }

            newHistorySeries.Seasons.Add(newSeason);
            newHistorySeries.UpdateNewEpisodes();
        }

        var sortedList = crunInstance.HistoryList.OrderBy(item => item.SeriesTitle).ToList();
        crunInstance.HistoryList.Clear();
        foreach (var item in sortedList){
            crunInstance.HistoryList.Add(item);
        }

        UpdateHistoryFile();
    }

    public async void UpdateWithSeasonData(CrunchyEpisodeList seasonData){
        if (seasonData.Data != null){
            var firstEpisode = seasonData.Data.First();
            var seriesId = firstEpisode.SeriesId;
            var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);
            if (historySeries != null){
                var historySeason = historySeries.Seasons.Find(s => s.SeasonId == firstEpisode.SeasonId);

                if (historySeason != null){
                    foreach (var crunchyEpisode in seasonData.Data){
                        if (historySeason.EpisodesList.All(e => e.EpisodeId != crunchyEpisode.Id)){
                            var newHistoryEpisode = new HistoryEpisode{
                                EpisodeTitle = crunchyEpisode.Title,
                                EpisodeId = crunchyEpisode.Id,
                                Episode = crunchyEpisode.Episode,
                            };

                            historySeason.EpisodesList.Add(newHistoryEpisode);
                        }
                    }

                    historySeason.EpisodesList.Sort(new NumericStringPropertyComparer());
                } else{
                    var newSeason = NewHistorySeason(seasonData, firstEpisode);

                    newSeason.EpisodesList.Sort(new NumericStringPropertyComparer());

                    historySeries.Seasons.Add(newSeason);

                    historySeries.Seasons = historySeries.Seasons.OrderBy(s => s.SeasonNum).ToList();
                }
                historySeries.UpdateNewEpisodes();
            } else{
                var newHistorySeries = new HistorySeries{
                    SeriesTitle = firstEpisode.SeriesTitle,
                    SeriesId = firstEpisode.SeriesId,
                    Seasons =[],
                };
                crunInstance.HistoryList.Add(newHistorySeries);

                var newSeason = NewHistorySeason(seasonData, firstEpisode);

                newSeason.EpisodesList.Sort(new NumericStringPropertyComparer());

                var series = await crunInstance.CrSeries.SeriesById(seriesId);
                if (series?.Data != null){
                    newHistorySeries.SeriesDescription = series.Data.First().Description;
                    newHistorySeries.ThumbnailImageUrl = GetSeriesThumbnail(series);
                }


                newHistorySeries.Seasons.Add(newSeason);
                newHistorySeries.UpdateNewEpisodes();
            }
        }

        var sortedList = crunInstance.HistoryList.OrderBy(item => item.SeriesTitle).ToList();
        crunInstance.HistoryList.Clear();
        foreach (var item in sortedList){
            crunInstance.HistoryList.Add(item);
        }

        UpdateHistoryFile();
    }

    private string GetSeriesThumbnail(CrSeriesBase series){
        // var series = await crunInstance.CrSeries.SeriesById(seriesId);

        if ((series.Data ?? Array.Empty<SeriesBaseItem>()).First().Images.PosterTall?.Count > 0){
            return series.Data.First().Images.PosterTall.First().First(e => e.Height == 360).Source;
        }

        return "";
    }

    private static bool CheckStringForSpecial(string identifier){
        // Regex pattern to match any sequence that does NOT contain "|S" followed by one or more digits immediately after
        string pattern = @"^(?!.*\|S\d+).*";

        // Use Regex.IsMatch to check if the identifier matches the pattern
        return Regex.IsMatch(identifier, pattern);
    }

    private static HistorySeason NewHistorySeason(CrunchyEpisodeList seasonData, CrunchyEpisode firstEpisode){
        var newSeason = new HistorySeason{
            SeasonTitle = firstEpisode.SeasonTitle,
            SeasonId = firstEpisode.SeasonId,
            SeasonNum = firstEpisode.SeasonNumber + "",
            EpisodesList =[],
            SpecialSeason = CheckStringForSpecial(firstEpisode.Identifier)
        };

        foreach (var crunchyEpisode in seasonData.Data!){
            var newHistoryEpisode = new HistoryEpisode{
                EpisodeTitle = crunchyEpisode.Title,
                EpisodeId = crunchyEpisode.Id,
                Episode = crunchyEpisode.Episode,
            };

            newSeason.EpisodesList.Add(newHistoryEpisode);
        }

        return newSeason;
    }

    private static HistorySeason NewHistorySeason(CrunchyEpisode episode){
        var newSeason = new HistorySeason{
            SeasonTitle = episode.SeasonTitle,
            SeasonId = episode.SeasonId,
            SeasonNum = episode.SeasonNumber + "",
            EpisodesList =[],
        };

        var newHistoryEpisode = new HistoryEpisode{
            EpisodeTitle = episode.Title,
            EpisodeId = episode.Id,
            Episode = episode.Episode,
        };

        newSeason.EpisodesList.Add(newHistoryEpisode);


        return newSeason;
    }
}

public class NumericStringPropertyComparer : IComparer<HistoryEpisode>{
    public int Compare(HistoryEpisode x, HistoryEpisode y){
        if (int.TryParse(x.Episode, out int xInt) && int.TryParse(y.Episode, out int yInt)){
            return xInt.CompareTo(yInt);
        }

        // Fall back to string comparison if not parseable as integers
        return String.Compare(x.Episode, y.Episode, StringComparison.Ordinal);
    }
}

public class HistorySeries : INotifyPropertyChanged{
    [JsonProperty("series_title")]
    public string? SeriesTitle{ get; set; }

    [JsonProperty("series_id")]
    public string? SeriesId{ get; set; }

    [JsonProperty("series_description")]
    public string? SeriesDescription{ get; set; }

    [JsonProperty("series_thumbnail_url")]
    public string? ThumbnailImageUrl{ get; set; }

    [JsonProperty("series_new_episodes")]
    public int NewEpisodes{ get; set; }

    [JsonIgnore]
    public Bitmap? ThumbnailImage{ get; set; }

    [JsonProperty("series_season_list")]
    public required List<HistorySeason> Seasons{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadImage(){
        try{
            using (var client = new HttpClient()){
                var response = await client.GetAsync(ThumbnailImageUrl);
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync()){
                    ThumbnailImage = new Bitmap(stream);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailImage)));
                }
            }
        } catch (Exception ex){
            // Handle exceptions
            Console.WriteLine("Failed to load image: " + ex.Message);
        }
    }

    public void UpdateNewEpisodes(){
        int count = 0;
        bool foundWatched = false;

        // Iterate over the Seasons list from the end to the beginning
        for (int i = Seasons.Count - 1; i >= 0 && !foundWatched; i--){

            if (Seasons[i].SpecialSeason == true){
                continue;
            }
            
            // Iterate over the Episodes from the end to the beginning
            for (int j = Seasons[i].EpisodesList.Count - 1; j >= 0 && !foundWatched; j--){
                if (!Seasons[i].EpisodesList[j].WasDownloaded){
                    count++;
                } else{
                    foundWatched = true;
                }
            }
        }
        NewEpisodes = count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewEpisodes)));
    }
    
    public async Task AddNewMissingToDownloads(){
        bool foundWatched = false;

        // Iterate over the Seasons list from the end to the beginning
        for (int i = Seasons.Count - 1; i >= 0 && !foundWatched; i--){

            if (Seasons[i].SpecialSeason == true){
                continue;
            }
            
            // Iterate over the Episodes from the end to the beginning
            for (int j = Seasons[i].EpisodesList.Count - 1; j >= 0 && !foundWatched; j--){
                if (!Seasons[i].EpisodesList[j].WasDownloaded){
                    //ADD to download queue
                    await Seasons[i].EpisodesList[j].DownloadEpisode();
                } else{
                    foundWatched = true;
                }
            }
        }
    }

    public async Task FetchData(string? seasonId){
        await Crunchyroll.Instance.CrHistory.UpdateSeries(SeriesId, seasonId);
    }
}

public class HistorySeason : INotifyPropertyChanged{
    [JsonProperty("season_title")]
    public string? SeasonTitle{ get; set; }

    [JsonProperty("season_id")]
    public string? SeasonId{ get; set; }

    [JsonProperty("season_cr_season_number")]
    public string? SeasonNum{ get; set; }
    
    [JsonProperty("season_special_season")]
    public bool? SpecialSeason{ get; set; }
    [JsonIgnore]
    public string CombinedProperty => SpecialSeason ?? false ? $"Specials {SeasonNum}" : $"Season {SeasonNum}";

    [JsonProperty("season_downloaded_episodes")]
    public int DownloadedEpisodes{ get; set; }

    [JsonProperty("season_episode_list")]
    public required List<HistoryEpisode> EpisodesList{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateDownloaded(string? EpisodeId){
        if (!string.IsNullOrEmpty(EpisodeId)){
            EpisodesList.First(e => e.EpisodeId == EpisodeId).ToggleWasDownloaded();
        }

        DownloadedEpisodes = EpisodesList.FindAll(e => e.WasDownloaded).Count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedEpisodes)));
        CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
    }

    public void UpdateDownloaded(){
        DownloadedEpisodes = EpisodesList.FindAll(e => e.WasDownloaded).Count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadedEpisodes)));
        CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, Crunchyroll.Instance.HistoryList);
    }
}

public partial class HistoryEpisode : INotifyPropertyChanged{
    [JsonProperty("episode_title")]
    public string? EpisodeTitle{ get; set; }

    [JsonProperty("episode_id")]
    public string? EpisodeId{ get; set; }

    [JsonProperty("episode_cr_episode_number")]
    public string? Episode{ get; set; }

    [JsonProperty("episode_was_downloaded")]
    public bool WasDownloaded{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ToggleWasDownloaded(){
        WasDownloaded = !WasDownloaded;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WasDownloaded)));
    }

    public async Task DownloadEpisode(){
        await Crunchyroll.Instance.AddEpisodeToQue(EpisodeId, Crunchyroll.Instance.DefaultLocale, Crunchyroll.Instance.CrunOptions.DubLang);

    }
}