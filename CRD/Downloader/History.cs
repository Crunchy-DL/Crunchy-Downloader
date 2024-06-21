using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CRD.Utils;
using CRD.Utils.Sonarr;
using CRD.Utils.Sonarr.Models;
using CRD.Utils.Structs;
using CRD.Views;
using Newtonsoft.Json;
using ReactiveUI;

namespace CRD.Downloader;

public class History(){
    private readonly Crunchyroll crunInstance = Crunchyroll.Instance;

    public async Task UpdateSeries(string seriesId, string? seasonId){
        await crunInstance.CrAuth.RefreshToken(true);

        CrSeriesSearch? parsedSeries = await crunInstance.CrSeries.ParseSeriesById(seriesId, "ja");

        if (parsedSeries == null){
            Console.Error.WriteLine("Parse Data Invalid");
            return;
        }

        var result = crunInstance.CrSeries.ParseSeriesResult(parsedSeries);
        Dictionary<string, EpisodeAndLanguage> episodes = new Dictionary<string, EpisodeAndLanguage>();

        foreach (int season in result.Keys){
            foreach (var key in result[season].Keys){
                var s = result[season][key];
                if (!string.IsNullOrEmpty(seasonId) && s.Id != seasonId) continue;

                var sId = s.Id;
                if (s.Versions is{ Count: > 0 }){
                    foreach (var sVersion in s.Versions){
                        if (sVersion.Original == true){
                            if (sVersion.Guid != null){
                                sId = sVersion.Guid;
                            }

                            break;
                        }
                    }
                }

                var seasonData = await crunInstance.CrSeries.GetSeasonDataById(sId);
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

    public HistoryEpisode? GetHistoryEpisode(string? seriesId, string? seasonId, string episodeId){
        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);

        if (historySeries != null){
            var historySeason = historySeries.Seasons.Find(s => s.SeasonId == seasonId);

            if (historySeason != null){
                var historyEpisode = historySeason.EpisodesList.Find(e => e.EpisodeId == episodeId);

                if (historyEpisode != null){
                    return historyEpisode;
                }
            }
        }

        return null;
    }

    public (HistoryEpisode? historyEpisode, string downloadDirPath) GetHistoryEpisodeWithDownloadDir(string? seriesId, string? seasonId, string episodeId){
        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);

        var downloadDirPath = "";
        
        if (historySeries != null){
            var historySeason = historySeries.Seasons.Find(s => s.SeasonId == seasonId);
            if (!string.IsNullOrEmpty(historySeries.SeriesDownloadPath)){
                downloadDirPath = historySeries.SeriesDownloadPath;
            }

            if (historySeason != null){
                var historyEpisode = historySeason.EpisodesList.Find(e => e.EpisodeId == episodeId);
                if (!string.IsNullOrEmpty(historySeason.SeasonDownloadPath)){
                    downloadDirPath = historySeason.SeasonDownloadPath;
                }

                if (historyEpisode != null){
                    return (historyEpisode, downloadDirPath);
                }
            }
        }

        return (null, downloadDirPath);
    }


    public async Task UpdateWithEpisode(CrunchyEpisode episodeParam){
        var episode = episodeParam;

        if (episode.Versions != null){
            var version = episode.Versions.Find(a => a.Original);
            if (version.AudioLocale != episode.AudioLocale){
                var crEpisode = await crunInstance.CrEpisode.ParseEpisodeById(version.Guid, "");
                if (crEpisode != null){
                    episode = crEpisode.Value;
                } else{
                    MessageBus.Current.SendMessage(new ToastMessage($"Couldn't update download History", ToastType.Warning, 1));
                    return;
                }
            }
        }


        var seriesId = episode.SeriesId;
        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);
        if (historySeries != null){
            var historySeason = historySeries.Seasons.Find(s => s.SeasonId == episode.SeasonId);

            var series = await crunInstance.CrSeries.SeriesById(seriesId);
            if (series?.Data != null){
                historySeries.SeriesTitle = series.Data.First().Title;
            }

            if (historySeason != null){
                historySeason.SeasonTitle = episode.SeasonTitle;
                historySeason.SeasonNum = Helpers.ExtractNumberAfterS(episode.Identifier) ?? episode.SeasonNumber + "";
                if (historySeason.EpisodesList.All(e => e.EpisodeId != episode.Id)){
                    var newHistoryEpisode = new HistoryEpisode{
                        EpisodeTitle = episode.Identifier.Contains("|M|") ? episode.SeasonTitle : episode.Title,
                        EpisodeDescription = episode.Description,
                        EpisodeId = episode.Id,
                        Episode = episode.Episode,
                        EpisodeSeasonNum = Helpers.ExtractNumberAfterS(episode.Identifier) ?? episode.SeasonNumber + "",
                        SpecialEpisode = !int.TryParse(episode.Episode, out _),
                    };

                    historySeason.EpisodesList.Add(newHistoryEpisode);

                    historySeason.EpisodesList.Sort(new NumericStringPropertyComparer());
                }
            } else{
                var newSeason = NewHistorySeason(episode);

                historySeries.Seasons.Add(newSeason);

                historySeries.Seasons = historySeries.Seasons.OrderBy(s => s.SeasonNum != null ? int.Parse(s.SeasonNum) : 0).ToList();
            }

            historySeries.UpdateNewEpisodes();
        } else{
            historySeries = new HistorySeries{
                SeriesTitle = episode.SeriesTitle,
                SeriesId = episode.SeriesId,
                Seasons =[],
            };
            crunInstance.HistoryList.Add(historySeries);
            var newSeason = NewHistorySeason(episode);

            var series = await crunInstance.CrSeries.SeriesById(seriesId);
            if (series?.Data != null){
                historySeries.SeriesDescription = series.Data.First().Description;
                historySeries.ThumbnailImageUrl = GetSeriesThumbnail(series);
                historySeries.SeriesTitle = series.Data.First().Title;
            }

            historySeries.Seasons.Add(newSeason);
            historySeries.UpdateNewEpisodes();
        }

        var sortedList = crunInstance.HistoryList.OrderBy(item => item.SeriesTitle).ToList();
        crunInstance.HistoryList.Clear();
        foreach (var item in sortedList){
            crunInstance.HistoryList.Add(item);
        }

        MatchHistorySeriesWithSonarr(false);
        await MatchHistoryEpisodesWithSonarr(false, historySeries);
        UpdateHistoryFile();
    }

    public async Task UpdateWithSeasonData(CrunchyEpisodeList seasonData){
        if (seasonData.Data != null){
            var firstEpisode = seasonData.Data.First();
            var seriesId = firstEpisode.SeriesId;
            var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);
            if (historySeries != null){
                var historySeason = historySeries.Seasons.Find(s => s.SeasonId == firstEpisode.SeasonId);
                var series = await crunInstance.CrSeries.SeriesById(seriesId);
                if (series?.Data != null){
                    historySeries.SeriesTitle = series.Data.First().Title;
                }

                if (historySeason != null){
                    historySeason.SeasonTitle = firstEpisode.SeasonTitle;
                    historySeason.SeasonNum = Helpers.ExtractNumberAfterS(firstEpisode.Identifier) ?? firstEpisode.SeasonNumber + "";
                    foreach (var crunchyEpisode in seasonData.Data){
                        var historyEpisode = historySeason.EpisodesList.Find(e => e.EpisodeId == crunchyEpisode.Id);

                        if (historyEpisode == null){
                            var newHistoryEpisode = new HistoryEpisode{
                                EpisodeTitle = crunchyEpisode.Identifier.Contains("|M|") ? crunchyEpisode.SeasonTitle : crunchyEpisode.Title,
                                EpisodeDescription = crunchyEpisode.Description,
                                EpisodeId = crunchyEpisode.Id,
                                Episode = crunchyEpisode.Episode,
                                EpisodeSeasonNum = Helpers.ExtractNumberAfterS(crunchyEpisode.Identifier) ?? crunchyEpisode.SeasonNumber + "",
                                SpecialEpisode = !int.TryParse(crunchyEpisode.Episode, out _),
                            };

                            historySeason.EpisodesList.Add(newHistoryEpisode);
                        } else{
                            //Update existing episode
                            historyEpisode.EpisodeTitle = crunchyEpisode.Identifier.Contains("|M|") ? crunchyEpisode.SeasonTitle : crunchyEpisode.Title;
                            historyEpisode.SpecialEpisode = !int.TryParse(crunchyEpisode.Episode, out _);
                            historyEpisode.EpisodeDescription = crunchyEpisode.Description;
                            historyEpisode.EpisodeId = crunchyEpisode.Id;
                            historyEpisode.Episode = crunchyEpisode.Episode;
                            historyEpisode.EpisodeSeasonNum = Helpers.ExtractNumberAfterS(crunchyEpisode.Identifier) ?? crunchyEpisode.SeasonNumber + "";
                        }
                    }

                    historySeason.EpisodesList.Sort(new NumericStringPropertyComparer());
                } else{
                    var newSeason = NewHistorySeason(seasonData, firstEpisode);

                    newSeason.EpisodesList.Sort(new NumericStringPropertyComparer());

                    historySeries.Seasons.Add(newSeason);

                    historySeries.Seasons = historySeries.Seasons.OrderBy(s => s.SeasonNum != null ? int.Parse(s.SeasonNum) : 0).ToList();
                }

                historySeries.UpdateNewEpisodes();
            } else{
                historySeries = new HistorySeries{
                    SeriesTitle = firstEpisode.SeriesTitle,
                    SeriesId = firstEpisode.SeriesId,
                    Seasons =[],
                };
                crunInstance.HistoryList.Add(historySeries);

                var newSeason = NewHistorySeason(seasonData, firstEpisode);

                newSeason.EpisodesList.Sort(new NumericStringPropertyComparer());

                var series = await crunInstance.CrSeries.SeriesById(seriesId);
                if (series?.Data != null){
                    historySeries.SeriesDescription = series.Data.First().Description;
                    historySeries.ThumbnailImageUrl = GetSeriesThumbnail(series);
                    historySeries.SeriesTitle = series.Data.First().Title;
                }


                historySeries.Seasons.Add(newSeason);

                historySeries.UpdateNewEpisodes();
            }

            var sortedList = crunInstance.HistoryList.OrderBy(item => item.SeriesTitle).ToList();
            crunInstance.HistoryList.Clear();
            foreach (var item in sortedList){
                crunInstance.HistoryList.Add(item);
            }

            MatchHistorySeriesWithSonarr(false);
            await MatchHistoryEpisodesWithSonarr(false, historySeries);
            UpdateHistoryFile();
        }
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
            SeasonNum = Helpers.ExtractNumberAfterS(firstEpisode.Identifier) ?? firstEpisode.SeasonNumber + "",
            EpisodesList =[],
            SpecialSeason = CheckStringForSpecial(firstEpisode.Identifier)
        };

        foreach (var crunchyEpisode in seasonData.Data!){
            var newHistoryEpisode = new HistoryEpisode{
                EpisodeTitle = crunchyEpisode.Identifier.Contains("|M|") ? crunchyEpisode.SeasonTitle : crunchyEpisode.Title,
                EpisodeDescription = crunchyEpisode.Description,
                EpisodeId = crunchyEpisode.Id,
                Episode = crunchyEpisode.Episode,
                EpisodeSeasonNum = Helpers.ExtractNumberAfterS(firstEpisode.Identifier) ?? firstEpisode.SeasonNumber + "",
                SpecialEpisode = !int.TryParse(crunchyEpisode.Episode, out _),
            };

            newSeason.EpisodesList.Add(newHistoryEpisode);
        }

        return newSeason;
    }

    private static HistorySeason NewHistorySeason(CrunchyEpisode episode){
        var newSeason = new HistorySeason{
            SeasonTitle = episode.SeasonTitle,
            SeasonId = episode.SeasonId,
            SeasonNum = Helpers.ExtractNumberAfterS(episode.Identifier) ?? episode.SeasonNumber + "",
            EpisodesList =[],
        };

        var newHistoryEpisode = new HistoryEpisode{
            EpisodeTitle = episode.Identifier.Contains("|M|") ? episode.SeasonTitle : episode.Title,
            EpisodeDescription = episode.Description,
            EpisodeId = episode.Id,
            Episode = episode.Episode,
            EpisodeSeasonNum = Helpers.ExtractNumberAfterS(episode.Identifier) ?? episode.SeasonNumber + "",
            SpecialEpisode = !int.TryParse(episode.Episode, out _),
        };

        newSeason.EpisodesList.Add(newHistoryEpisode);


        return newSeason;
    }

    public void MatchHistorySeriesWithSonarr(bool updateAll){
        if (crunInstance.CrunOptions.SonarrProperties is{ SonarrEnabled: false }){
            return;
        }

        foreach (var historySeries in crunInstance.HistoryList){
            if (updateAll || string.IsNullOrEmpty(historySeries.SonarrSeriesId)){
                var sonarrSeries = FindClosestMatch(historySeries.SeriesTitle);
                if (sonarrSeries != null){
                    historySeries.SonarrSeriesId = sonarrSeries.Id + "";
                    historySeries.SonarrTvDbId = sonarrSeries.TvdbId + "";
                    historySeries.SonarrSlugTitle = sonarrSeries.TitleSlug;
                }
            }
        }
    }

    public async Task MatchHistoryEpisodesWithSonarr(bool updateAll, HistorySeries historySeries){
        if (crunInstance.CrunOptions.SonarrProperties is{ SonarrEnabled: false }){
            return;
        }

        if (!string.IsNullOrEmpty(historySeries.SonarrSeriesId)){
            var episodes = await SonarrClient.Instance.GetEpisodes(int.Parse(historySeries.SonarrSeriesId));

            List<HistoryEpisode> allHistoryEpisodes =[];

            foreach (var historySeriesSeason in historySeries.Seasons){
                allHistoryEpisodes.AddRange(historySeriesSeason.EpisodesList);
            }

            List<HistoryEpisode> failedEpisodes =[];

            foreach (var historyEpisode in allHistoryEpisodes){
                if (updateAll || string.IsNullOrEmpty(historyEpisode.SonarrEpisodeId)){
                    var episode = FindClosestMatchEpisodes(episodes, historyEpisode.EpisodeTitle);
                    if (episode != null){
                        historyEpisode.SonarrEpisodeId = episode.Id + "";
                        historyEpisode.SonarrEpisodeNumber = episode.EpisodeNumber + "";
                        historyEpisode.SonarrHasFile = episode.HasFile;
                        historyEpisode.SonarrAbsolutNumber = episode.AbsoluteEpisodeNumber + "";
                        historyEpisode.SonarrSeasonNumber = episode.SeasonNumber + "";
                        episodes.Remove(episode);
                    } else{
                        failedEpisodes.Add(historyEpisode);
                    }
                }
            }

            foreach (var historyEpisode in failedEpisodes){
                var episode = episodes.Find(ele => ele.EpisodeNumber + "" == historyEpisode.Episode && ele.SeasonNumber + "" == historyEpisode.EpisodeSeasonNum);
                if (episode != null){
                    historyEpisode.SonarrEpisodeId = episode.Id + "";
                    historyEpisode.SonarrEpisodeNumber = episode.EpisodeNumber + "";
                    historyEpisode.SonarrHasFile = episode.HasFile;
                    historyEpisode.SonarrAbsolutNumber = episode.AbsoluteEpisodeNumber + "";
                    historyEpisode.SonarrSeasonNumber = episode.SeasonNumber + "";
                    episodes.Remove(episode);
                } else{
                    var episode1 = episodes.Find(ele =>
                        !string.IsNullOrEmpty(historyEpisode.EpisodeDescription) && !string.IsNullOrEmpty(ele.Overview) && Helpers.CalculateCosineSimilarity(ele.Overview, historyEpisode.EpisodeDescription) > 0.8);

                    if (episode1 != null){
                        historyEpisode.SonarrEpisodeId = episode1.Id + "";
                        historyEpisode.SonarrEpisodeNumber = episode1.EpisodeNumber + "";
                        historyEpisode.SonarrHasFile = episode1.HasFile;
                        historyEpisode.SonarrAbsolutNumber = episode1.AbsoluteEpisodeNumber + "";
                        historyEpisode.SonarrSeasonNumber = episode1.SeasonNumber + "";
                        episodes.Remove(episode1);
                    } else{
                        var episode2 = episodes.Find(ele => ele.AbsoluteEpisodeNumber + "" == historyEpisode.Episode);
                        if (episode2 != null){
                            historyEpisode.SonarrEpisodeId = episode2.Id + "";
                            historyEpisode.SonarrEpisodeNumber = episode2.EpisodeNumber + "";
                            historyEpisode.SonarrHasFile = episode2.HasFile;
                            historyEpisode.SonarrAbsolutNumber = episode2.AbsoluteEpisodeNumber + "";
                            historyEpisode.SonarrSeasonNumber = episode2.SeasonNumber + "";
                            episodes.Remove(episode2);
                        } else{
                            Console.Error.WriteLine($"Could not match episode {historyEpisode.EpisodeTitle} to sonarr episode");
                        }
                    }
                }
            }
        }
    }

    private SonarrSeries? FindClosestMatch(string title){
        SonarrSeries? closestMatch = null;
        double highestSimilarity = 0.0;

        Parallel.ForEach(crunInstance.SonarrSeries, series => {
            double similarity = CalculateSimilarity(series.Title.ToLower(), title.ToLower());
            if (similarity > highestSimilarity){
                highestSimilarity = similarity;
                closestMatch = series;
            }
        });

        return highestSimilarity < 0.8 ? null : closestMatch;
    }

    public SonarrEpisode? FindClosestMatchEpisodes(List<SonarrEpisode> episodeList, string title){
        SonarrEpisode? closestMatch = null;
        double highestSimilarity = 0.0;
        object lockObject = new object(); // To synchronize access to shared variables

        Parallel.ForEach(episodeList, episode => {
            double similarity = CalculateSimilarity(episode.Title, title);
            lock (lockObject) // Ensure thread-safe access to shared variables
            {
                if (similarity > highestSimilarity){
                    highestSimilarity = similarity;
                    closestMatch = episode;
                }
            }
        });

        return highestSimilarity < 0.8 ? null : closestMatch;
    }

    private double CalculateSimilarity(string source, string target){
        int distance = LevenshteinDistance(source, target);
        return 1.0 - (double)distance / Math.Max(source.Length, target.Length);
    }


    public int LevenshteinDistance(string source, string target){
        if (string.IsNullOrEmpty(source)){
            return string.IsNullOrEmpty(target) ? 0 : target.Length;
        }

        if (string.IsNullOrEmpty(target)){
            return source.Length;
        }

        int n = source.Length;
        int m = target.Length;

        // Use a single array for distances.
        int[] distances = new int[m + 1];

        // Initialize the distance array.
        for (int j = 0; j <= m; j++){
            distances[j] = j;
        }

        for (int i = 1; i <= n; i++){
            int previousDiagonal = distances[0];
            distances[0] = i;

            for (int j = 1; j <= m; j++){
                int previousDistance = distances[j];
                int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                distances[j] = Math.Min(
                    Math.Min(distances[j - 1] + 1, distances[j] + 1),
                    previousDiagonal + cost);

                previousDiagonal = previousDistance;
            }
        }

        // The final distance is in the last cell.
        return distances[m];
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

    [JsonProperty("sonarr_series_id")]
    public string? SonarrSeriesId{ get; set; }

    [JsonProperty("sonarr_tvdb_id")]
    public string? SonarrTvDbId{ get; set; }

    [JsonProperty("sonarr_slug_title")]
    public string? SonarrSlugTitle{ get; set; }

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

    [JsonProperty("series_download_path")]
    public string? SeriesDownloadPath{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    [JsonIgnore]
    public bool FetchingData{ get; set; }

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
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
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
                if (Seasons[i].EpisodesList[j].SpecialEpisode){
                    continue;
                }

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

    public void SetFetchingData(){
        FetchingData = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
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
                if (Seasons[i].EpisodesList[j].SpecialEpisode){
                    continue;
                }

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
        FetchingData = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
        await Crunchyroll.Instance.CrHistory.UpdateSeries(SeriesId, seasonId);
        FetchingData = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FetchingData)));
        Crunchyroll.Instance.CrHistory.MatchHistoryEpisodesWithSonarr(false, this);
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

    [JsonProperty("series_download_path")]
    public string? SeasonDownloadPath{ get; set; }

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

    [JsonProperty("episode_cr_episode_description")]
    public string? EpisodeDescription{ get; set; }

    [JsonProperty("episode_cr_season_number")]
    public string? EpisodeSeasonNum{ get; set; }

    [JsonProperty("episode_was_downloaded")]
    public bool WasDownloaded{ get; set; }

    [JsonProperty("episode_special_episode")]
    public bool SpecialEpisode{ get; set; }

    [JsonProperty("sonarr_episode_id")]
    public string? SonarrEpisodeId{ get; set; }

    [JsonProperty("sonarr_has_file")]
    public bool SonarrHasFile{ get; set; }

    [JsonProperty("sonarr_episode_number")]
    public string? SonarrEpisodeNumber{ get; set; }

    [JsonProperty("sonarr_season_number")]
    public string? SonarrSeasonNumber{ get; set; }

    [JsonProperty("sonarr_absolut_number")]
    public string? SonarrAbsolutNumber{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ToggleWasDownloaded(){
        WasDownloaded = !WasDownloaded;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WasDownloaded)));
    }

    public async Task DownloadEpisode(){
        await Crunchyroll.Instance.AddEpisodeToQue(EpisodeId, Crunchyroll.Instance.DefaultLocale, Crunchyroll.Instance.CrunOptions.DubLang);
    }
}