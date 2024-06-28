using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using CRD.Utils;
using CRD.Utils.Sonarr;
using CRD.Utils.Sonarr.Models;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Views;
using DynamicData;
using Newtonsoft.Json;
using ReactiveUI;

namespace CRD.Downloader;

public class History(){
    private readonly Crunchyroll crunInstance = Crunchyroll.Instance;

    public async Task UpdateSeries(string seriesId, string? seasonId){
        await crunInstance.CrAuth.RefreshToken(true);

        CrSeriesSearch? parsedSeries = await crunInstance.CrSeries.ParseSeriesById(seriesId, "ja-JP", true);

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

                var seasonData = await crunInstance.CrSeries.GetSeasonDataById(sId,string.IsNullOrEmpty(crunInstance.CrunOptions.HistoryLang) ? crunInstance.DefaultLocale : crunInstance.CrunOptions.HistoryLang,true);
                await UpdateWithSeasonData(seasonData);
            }
        }
    }

    private void UpdateHistoryFile(){
        CfgManager.WriteJsonToFile(CfgManager.PathCrHistory, crunInstance.HistoryList);
    }

    public void SetAsDownloaded(string? seriesId, string? seasonId, string episodeId){
        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);

        if (historySeries != null){
            var historySeason = historySeries.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);

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
            var historySeason = historySeries.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);

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
            var historySeason = historySeries.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
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
            var historySeason = historySeries.Seasons.FirstOrDefault(s => s.SeasonId == episode.SeasonId);

            await RefreshSeriesData(seriesId, historySeries);

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

                SortSeasons(historySeries);
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

            await RefreshSeriesData(seriesId, historySeries);

            historySeries.Seasons.Add(newSeason);
            historySeries.UpdateNewEpisodes();
        }

        SortItems();

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
                var historySeason = historySeries.Seasons.FirstOrDefault(s => s.SeasonId == firstEpisode.SeasonId);
                
                await RefreshSeriesData(seriesId, historySeries);

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

                    SortSeasons(historySeries);
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

                await RefreshSeriesData(seriesId, historySeries);


                historySeries.Seasons.Add(newSeason);

                historySeries.UpdateNewEpisodes();
            }

            SortItems();

            MatchHistorySeriesWithSonarr(false);
            await MatchHistoryEpisodesWithSonarr(false, historySeries);
            UpdateHistoryFile();
        }
    }

    private async Task RefreshSeriesData(string seriesId, HistorySeries historySeries){
        var series = await crunInstance.CrSeries.SeriesById(seriesId,string.IsNullOrEmpty(crunInstance.CrunOptions.HistoryLang) ? crunInstance.DefaultLocale : crunInstance.CrunOptions.HistoryLang,true);
        if (series?.Data != null){
            historySeries.SeriesDescription = series.Data.First().Description;
            historySeries.ThumbnailImageUrl = GetSeriesThumbnail(series);
            historySeries.SeriesTitle = series.Data.First().Title;
        }
    }

    private void SortSeasons(HistorySeries series){
        var sortedSeasons = series.Seasons
            .OrderBy(s => s.SeasonNum != null ? int.Parse(s.SeasonNum) : 0)
            .ToList();

        series.Seasons.Clear();

        foreach (var season in sortedSeasons){
            series.Seasons.Add(season);
        }
    }
    
    public void SortItems(){
        var currentSortingType = Crunchyroll.Instance.CrunOptions.HistoryPageProperties?.SelectedSorting ?? SortingType.SeriesTitle;
        switch (currentSortingType){
            case SortingType.SeriesTitle:
                var sortedList = Crunchyroll.Instance.HistoryList.OrderBy(s => s.SeriesTitle).ToList();

                Crunchyroll.Instance.HistoryList.Clear();

                Crunchyroll.Instance.HistoryList.AddRange(sortedList);


                return;

            case SortingType.NextAirDate:

                DateTime today = DateTime.UtcNow.Date;

                var sortedSeriesDates = Crunchyroll.Instance.HistoryList
                    .OrderByDescending(s => s.SonarrNextAirDate == "Today")
                    .ThenBy(s => s.SonarrNextAirDate == "Today" ? s.SeriesTitle : null)
                    .ThenBy(s => {
                        var date = ParseDate(s.SonarrNextAirDate, today);
                        return date.HasValue ? date.Value : DateTime.MaxValue;
                    })
                    .ThenBy(s => s.SeriesTitle)
                    .ToList();

                Crunchyroll.Instance.HistoryList.Clear();

                Crunchyroll.Instance.HistoryList.AddRange(sortedSeriesDates);


                return;
        }
    }

    public static DateTime? ParseDate(string dateStr, DateTime today){
        if (dateStr == "Today"){
            return today;
        }

        if (DateTime.TryParseExact(dateStr, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)){
            return date;
        }

        return null;
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

            historySeries.SonarrNextAirDate = GetNextAirDate(episodes);
            
            List<HistoryEpisode> allHistoryEpisodes =[];

            foreach (var historySeriesSeason in historySeries.Seasons){
                allHistoryEpisodes.AddRange(historySeriesSeason.EpisodesList);
            }

            List<HistoryEpisode> failedEpisodes =[];

            Parallel.ForEach(allHistoryEpisodes, historyEpisode =>
            {
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
            });

            Parallel.ForEach(failedEpisodes, historyEpisode =>
            {
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
            });
        }
    }

    private string GetNextAirDate(List<SonarrEpisode> episodes){
        DateTime today = DateTime.UtcNow.Date;

        // Check if any episode air date matches today
        var todayEpisode = episodes.FirstOrDefault(e => e.AirDateUtc.Date == today);
        if (todayEpisode != null){
            return "Today";
        }

        // Find the next episode date
        var nextEpisode = episodes
            .Where(e => e.AirDateUtc.Date > today)
            .OrderBy(e => e.AirDateUtc.Date)
            .FirstOrDefault();

        if (nextEpisode != null){
            return nextEpisode.AirDateUtc.ToString("dd.MM.yyyy");
        }

        // If no future episode date is found
        return string.Empty;
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

