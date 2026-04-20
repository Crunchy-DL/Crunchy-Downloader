using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CRD.Downloader.Crunchyroll;
using CRD.Downloader.Crunchyroll.Utils;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Sonarr;
using CRD.Utils.Sonarr.Models;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll.Music;
using CRD.Utils.Structs.History;
using CRD.Views;
using DynamicData;
using ReactiveUI;

namespace CRD.Downloader;

public class History{
    private readonly CrunchyrollManager crunInstance = CrunchyrollManager.Instance;

    public async Task<bool> CrUpdateSeries(string? seriesId, string? seasonId){
        if (string.IsNullOrEmpty(seriesId)){
            return false;
        }

        await crunInstance.CrAuthEndpoint1.RefreshToken(true);

        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);

        if (historySeries != null){
            if (string.IsNullOrEmpty(seasonId)){
                foreach (var historySeriesSeason in historySeries.Seasons){
                    foreach (var historyEpisode in historySeriesSeason.EpisodesList){
                        historyEpisode.IsEpisodeAvailableOnStreamingService = false;
                    }
                }
            } else{
                var matchingSeason = historySeries.Seasons.FirstOrDefault(historySeason => historySeason.SeasonId == seasonId);

                if (matchingSeason != null){
                    foreach (var historyEpisode in matchingSeason.EpisodesList){
                        historyEpisode.IsEpisodeAvailableOnStreamingService = false;
                    }
                }
            }
        }


        CrSeriesSearch? parsedSeries = await crunInstance.CrSeries.ParseSeriesById(seriesId, "ja-JP", true);

        if (parsedSeries == null){
            Console.Error.WriteLine("Parse Data Invalid - series is maybe only available with VPN or got deleted");
            return false;
        }

        if (parsedSeries.Data != null){
            var result = false;
            foreach (var s in parsedSeries.Data){
                var lang = string.IsNullOrEmpty(crunInstance.CrunOptions.HistoryLang)
                    ? crunInstance.DefaultLocale
                    : crunInstance.CrunOptions.HistoryLang;

                var candidateIds = new List<string>();

                if (s.Versions is{ Count: > 0 }){
                    candidateIds.AddRange(
                        s.Versions
                            .Where(v => v.Original == true && !string.IsNullOrWhiteSpace(v.Guid))
                            .OrderByDescending(v => v.Guid!.Length)
                            .Select(v => v.Guid!)
                    );
                }

                if (!string.IsNullOrWhiteSpace(s.Id)){
                    candidateIds.Add(s.Id);
                }

                candidateIds = candidateIds
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!string.IsNullOrEmpty(seasonId) &&
                    !candidateIds.Contains(seasonId, StringComparer.OrdinalIgnoreCase)){
                    continue;
                }

                foreach (var candidateId in candidateIds){
                    try{
                        var seasonData = await crunInstance.CrSeries.GetSeasonDataById(candidateId, lang, true);

                        if (seasonData.Data is{ Count: > 0 }){
                            result = true;
                            await crunInstance.History.UpdateWithSeasonData(seasonData.Data.ToList<IHistorySource>());
                            break;
                        }
                    } catch{
                        // optional: log candidateId
                    }
                }
            }

            historySeries ??= crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);

            if (historySeries != null){
                RemoveUnavailableEpisodes(historySeries);
                if (historySeries.Seasons.Count == 0){
                    crunInstance.HistoryList.Remove(historySeries);
                    CfgManager.UpdateHistoryFile();
                    return result;
                }

                MatchHistorySeriesWithSonarr(false);
                await MatchHistoryEpisodesWithSonarr(false, historySeries);
                CfgManager.UpdateHistoryFile();
                return result;
            }
        }

        return false;
    }


    public async Task UpdateWithMusicEpisodeList(List<CrunchyMusicVideo> episodeList){
        if (episodeList is{ Count: > 0 }){
            if (crunInstance.CrunOptions is{ History: true, HistoryIncludeCrArtists: true }){
                var concertGroups = episodeList.Where(e => e.EpisodeType == EpisodeType.Concert).GroupBy(e => e.Artist.Id);
                var musicVideoGroups = episodeList.Where(e => e.EpisodeType == EpisodeType.MusicVideo).GroupBy(e => e.Artist.Id);

                foreach (var concertGroup in concertGroups){
                    await UpdateWithSeasonData(concertGroup.ToList<IHistorySource>());
                }

                foreach (var musicVideoGroup in musicVideoGroups){
                    await UpdateWithSeasonData(musicVideoGroup.ToList<IHistorySource>());
                }
            }
        }
    }

    public async Task UpdateWithEpisodeList(List<CrunchyEpisode> episodeList){
        if (episodeList is{ Count: > 0 }){
            var episodeVersions = episodeList.First().Versions;
            if (episodeVersions != null){
                var version = episodeVersions.Find(a => a.Original);
                if (version?.AudioLocale != episodeList.First().AudioLocale){
                    await CrUpdateSeries(episodeList.First().SeriesId, version?.SeasonGuid);
                    return;
                }
            } else{
                await CrUpdateSeries(episodeList.First().SeriesId, "");
                return;
            }

            await UpdateWithSeasonData(episodeList.ToList<IHistorySource>());
        }
    }

    public async Task UpdateWithEpisode(List<CrBrowseEpisode> episodes){
        var historyIndex = crunInstance.HistoryList
            .Where(h => !string.IsNullOrWhiteSpace(h.SeriesId))
            .ToDictionary(
                h => h.SeriesId!,
                h => (h.Seasons)
                    .Where(s => !string.IsNullOrWhiteSpace(s.SeasonId))
                    .ToDictionary(
                        s => s.SeasonId ?? "UNKNOWN",
                        s => (s.EpisodesList)
                            .Select(ep => ep.EpisodeId)
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .ToHashSet(StringComparer.Ordinal),
                        StringComparer.Ordinal
                    ),
                StringComparer.Ordinal
            );

        episodes = episodes
            .Where(e => !string.IsNullOrWhiteSpace(e.EpisodeMetadata?.SeriesId) &&
                        historyIndex.ContainsKey(e.EpisodeMetadata!.SeriesId!))
            .ToList();

        foreach (var seriesGroup in episodes.GroupBy(e => e.EpisodeMetadata?.SeriesId ?? "UNKNOWN_SERIES")){
            var seriesId = seriesGroup.Key;

            var originalEntries = seriesGroup
                .Select(e => new{ OriginalId = TryGetOriginalId(e), SeasonId = TryGetOriginalSeasonId(e) })
                .Where(x => !string.IsNullOrWhiteSpace(x.OriginalId))
                .GroupBy(x => x.OriginalId!, StringComparer.Ordinal)
                .Select(g => new{
                    OriginalId = g.Key,
                    SeasonId = g.Select(x => x.SeasonId).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                })
                .ToList();

            var hasAnyOriginalInfo = originalEntries.Count > 0;

            var allOriginalsInHistory =
                hasAnyOriginalInfo
                && originalEntries.All(x => IsOriginalInHistory(historyIndex, seriesId, x.SeasonId, x.OriginalId));

            var originalItems = seriesGroup.Where(IsOriginalItem).ToList();

            if (originalItems.Count > 0){
                if (allOriginalsInHistory){
                    var sT = seriesGroup.Select(e => e.EpisodeMetadata?.SeriesTitle)
                        .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
                    // Console.WriteLine($"[INFO] Skipping SeriesId={seriesId} {sT} - all ORIGINAL episodes already in history.");
                    continue;
                }

                var convertedList = originalItems.Select(crBrowseEpisode => crBrowseEpisode.ToCrunchyEpisode()).ToList();

                await crunInstance.History.UpdateWithSeasonData(convertedList.ToList<IHistorySource>());

                continue;
            }

            var seriesTitle = seriesGroup.Select(e => e.EpisodeMetadata?.SeriesTitle)
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";

            if (allOriginalsInHistory){
                // Console.WriteLine($"[INFO] Skipping SeriesId={seriesId} - originals implied by Versions already in history.");
                continue;
            }

            Console.WriteLine($"[WARN] No original ITEM found for SeriesId={seriesId} {seriesTitle}");

            if (HasAllSeriesEpisodesInHistory(historyIndex, seriesId, seriesGroup)){
                Console.WriteLine($"[History] Skip (already in history): {seriesId}");
            } else{
                await CrUpdateSeries(seriesId, null);
                Console.WriteLine($"[History] Updating (full series): {seriesId}");
            }
        }

        return;
    }

    private string? TryGetOriginalId(CrBrowseEpisode e) =>
        e.EpisodeMetadata?.versions?
            .FirstOrDefault(v => v.Original && !string.IsNullOrWhiteSpace(v.Guid))
            ?.Guid;

    private string? TryGetOriginalSeasonId(CrBrowseEpisode e) =>
        e.EpisodeMetadata?.versions?
            .FirstOrDefault(v => v.Original && !string.IsNullOrWhiteSpace(v.SeasonGuid))
            ?.SeasonGuid
        ?? e.EpisodeMetadata?.SeasonId;

    private bool IsOriginalItem(CrBrowseEpisode e){
        var originalId = TryGetOriginalId(e);
        return !string.IsNullOrWhiteSpace(originalId)
               && !string.IsNullOrWhiteSpace(e.Id)
               && string.Equals(e.Id, originalId, StringComparison.Ordinal);
    }


    private bool IsOriginalInHistory(Dictionary<string, Dictionary<string, HashSet<string?>>> historyIndex, string seriesId, string? seasonId, string originalEpisodeId){
        if (!historyIndex.TryGetValue(seriesId, out var seasons)) return false;

        if (!string.IsNullOrWhiteSpace(seasonId))
            return seasons.TryGetValue(seasonId, out var eps) && eps.Contains(originalEpisodeId);

        return seasons.Values.Any(eps => eps.Contains(originalEpisodeId));
    }

    private bool HasAllSeriesEpisodesInHistory(Dictionary<string, Dictionary<string, HashSet<string?>>> historyIndex, string seriesId, IEnumerable<CrBrowseEpisode> seriesEpisodes){
        if (!historyIndex.TryGetValue(seriesId, out var seasons)) return false;

        var allHistoryEpisodeIds = seasons.Values
            .SelectMany(set => set)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var e in seriesEpisodes){
            if (string.IsNullOrWhiteSpace(e.Id)) return false;
            if (!allHistoryEpisodeIds.Contains(e.Id)) return false;
        }

        return true;
    }

    /// <summary>
    /// This method updates the History with a list of episodes. The episodes have to be from the same season.
    /// </summary>
    private async Task UpdateWithSeasonData(List<IHistorySource> episodeList){
        if (episodeList is{ Count: > 0 }){
            var firstEpisode = episodeList.First();
            var seriesId = firstEpisode.GetSeriesId();
            var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);
            if (historySeries != null){
                historySeries.HistorySeriesAddDate ??= DateTime.Now;
                historySeries.SeriesStreamingService = StreamingService.Crunchyroll;
                var historySeason = historySeries.Seasons.FirstOrDefault(s => s.SeasonId == firstEpisode.GetSeasonId());

                if (historySeason != null){
                    historySeason.SeasonTitle = firstEpisode.GetSeasonTitle();
                    historySeason.SeasonNum = firstEpisode.GetSeasonNum();
                    historySeason.SpecialSeason = firstEpisode.IsSpecialSeason();
                    foreach (var historySource in episodeList){
                        if (historySource.GetSeasonId() != historySeason.SeasonId){
                            continue;
                        }

                        var historyEpisode = historySeason.EpisodesList.Find(e => e.EpisodeId == historySource.GetEpisodeId());

                        if (historyEpisode == null){
                            var newHistoryEpisode = new HistoryEpisode{
                                EpisodeTitle = historySource.GetEpisodeTitle(),
                                EpisodeDescription = historySource.GetEpisodeDescription(),
                                EpisodeId = historySource.GetEpisodeId(),
                                Episode = historySource.GetEpisodeNumber(),
                                EpisodeSeasonNum = historySource.GetSeasonNum(),
                                SpecialEpisode = historySource.IsSpecialEpisode(),
                                HistoryEpisodeAvailableDubLang = historySource.GetEpisodeAvailableDubLang(),
                                HistoryEpisodeAvailableSoftSubs = historySource.GetEpisodeAvailableSoftSubs(),
                                EpisodeCrPremiumAirDate = historySource.GetAvailableDate(),
                                EpisodeType = historySource.GetEpisodeType(),
                                IsEpisodeAvailableOnStreamingService = true,
                                ThumbnailImageUrl = historySource.GetImageUrl(),
                            };

                            historySeason.EpisodesList.Add(newHistoryEpisode);
                        } else{
                            //Update existing episode
                            historyEpisode.EpisodeTitle = historySource.GetEpisodeTitle();
                            historyEpisode.SpecialEpisode = historySource.IsSpecialEpisode();
                            historyEpisode.EpisodeDescription = historySource.GetEpisodeDescription();
                            historyEpisode.EpisodeId = historySource.GetEpisodeId();
                            historyEpisode.Episode = historySource.GetEpisodeNumber();
                            historyEpisode.EpisodeSeasonNum = historySource.GetSeasonNum();
                            historyEpisode.EpisodeCrPremiumAirDate = historySource.GetAvailableDate();
                            historyEpisode.EpisodeType = historySource.GetEpisodeType();
                            historyEpisode.EpisodeSeriesType = historySource.GetSeriesType();
                            historyEpisode.IsEpisodeAvailableOnStreamingService = true;
                            historyEpisode.ThumbnailImageUrl = historySource.GetImageUrl();

                            historyEpisode.HistoryEpisodeAvailableDubLang = historySource.GetEpisodeAvailableDubLang();
                            historyEpisode.HistoryEpisodeAvailableSoftSubs = historySource.GetEpisodeAvailableSoftSubs();
                        }
                    }

                    historySeason.EpisodesList.Sort(new NumericStringPropertyComparer());
                } else{
                    var newSeason = NewHistorySeason(episodeList, firstEpisode);

                    newSeason.EpisodesList.Sort(new NumericStringPropertyComparer());

                    historySeries.Seasons.Add(newSeason);
                    newSeason.Init();
                }

                historySeries.SeriesType = InferSeriesType(historySeries);
                await RefreshSeriesData(seriesId, historySeries);
                _ = historySeries.LoadImage();
                historySeries.UpdateNewEpisodes();
            } else if (!string.IsNullOrEmpty(seriesId)){
                historySeries = new HistorySeries{
                    SeriesTitle = firstEpisode.GetSeriesTitle(),
                    SeriesId = firstEpisode.GetSeriesId(),
                    Seasons = [],
                    HistorySeriesAddDate = DateTime.Now,
                    SeriesType = SeriesType.Unknown,
                    SeriesStreamingService = StreamingService.Crunchyroll
                };
                crunInstance.HistoryList.Add(historySeries);

                var newSeason = NewHistorySeason(episodeList, firstEpisode);

                newSeason.EpisodesList.Sort(new NumericStringPropertyComparer());

                historySeries.Seasons.Add(newSeason);
                historySeries.SeriesType = InferSeriesType(historySeries);
                await RefreshSeriesData(seriesId, historySeries);
                _ = historySeries.LoadImage();
                historySeries.UpdateNewEpisodes();
                historySeries.Init();
                newSeason.Init();
            }

            SortItems();
            if (historySeries != null){
                SortSeasons(historySeries);
            }
        }
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

        MessageBus.Current.SendMessage(new ToastMessage($"Couldn't update download History", ToastType.Warning, 2));
    }

    public HistoryEpisode? GetHistoryEpisode(string? seriesId, string? seasonId, string episodeId){
        return CrunchyrollManager.Instance.HistoryList
            .FirstOrDefault(series => series.SeriesId == seriesId)?
            .Seasons.FirstOrDefault(season => season.SeasonId == seasonId)?
            .EpisodesList.Find(e => e.EpisodeId == episodeId);
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

    public (HistoryEpisode? historyEpisode, List<string> dublist, List<string> sublist, string downloadDirPath, string videoQuality) GetHistoryEpisodeWithDubListAndDownloadDir(string? seriesId, string? seasonId,
        string episodeId){
        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);

        var downloadDirPath = "";
        var videoQuality = "";
        List<string> dublist = [];
        List<string> sublist = [];

        if (historySeries != null){
            var historySeason = historySeries.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
            if (historySeries.HistorySeriesDubLangOverride.Count > 0){
                dublist = historySeries.HistorySeriesDubLangOverride.ToList();
            }

            if (historySeries.HistorySeriesSoftSubsOverride.Count > 0){
                sublist = historySeries.HistorySeriesSoftSubsOverride.ToList();
            }

            if (!string.IsNullOrEmpty(historySeries.SeriesDownloadPath)){
                downloadDirPath = historySeries.SeriesDownloadPath;
            }

            if (!string.IsNullOrEmpty(historySeries.HistorySeriesVideoQualityOverride)){
                videoQuality = historySeries.HistorySeriesVideoQualityOverride;
            }

            if (historySeason != null){
                var historyEpisode = historySeason.EpisodesList.Find(e => e.EpisodeId == episodeId);
                if (historySeason.HistorySeasonDubLangOverride.Count > 0){
                    dublist = historySeason.HistorySeasonDubLangOverride.ToList();
                }

                if (historySeason.HistorySeasonSoftSubsOverride.Count > 0){
                    sublist = historySeason.HistorySeasonSoftSubsOverride.ToList();
                }

                if (!string.IsNullOrEmpty(historySeason.SeasonDownloadPath)){
                    downloadDirPath = historySeason.SeasonDownloadPath;
                }

                if (!string.IsNullOrEmpty(historySeason.HistorySeasonVideoQualityOverride)){
                    videoQuality = historySeason.HistorySeasonVideoQualityOverride;
                }

                if (historyEpisode != null){
                    return (historyEpisode, dublist, sublist, downloadDirPath, videoQuality);
                }
            }
        }

        return (null, dublist, sublist, downloadDirPath, videoQuality);
    }

    public List<string> GetDubList(string? seriesId, string? seasonId){
        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);

        List<string> dublist = [];

        if (historySeries != null){
            var historySeason = historySeries.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
            if (historySeries.HistorySeriesDubLangOverride.Count > 0){
                dublist = historySeries.HistorySeriesDubLangOverride.ToList();
            }

            if (historySeason is{ HistorySeasonDubLangOverride.Count: > 0 }){
                dublist = historySeason.HistorySeasonDubLangOverride.ToList();
            }
        }

        return dublist;
    }

    public (List<string> sublist, string videoQuality) GetSubList(string? seriesId, string? seasonId){
        var historySeries = crunInstance.HistoryList.FirstOrDefault(series => series.SeriesId == seriesId);

        List<string> sublist = [];
        var videoQuality = "";

        if (historySeries != null){
            var historySeason = historySeries.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
            if (historySeries.HistorySeriesSoftSubsOverride.Count > 0){
                sublist = historySeries.HistorySeriesSoftSubsOverride.ToList();
            }

            if (!string.IsNullOrEmpty(historySeries.HistorySeriesVideoQualityOverride)){
                videoQuality = historySeries.HistorySeriesVideoQualityOverride;
            }

            if (historySeason is{ HistorySeasonSoftSubsOverride.Count: > 0 }){
                sublist = historySeason.HistorySeasonSoftSubsOverride.ToList();
            }

            if (historySeason != null && !string.IsNullOrEmpty(historySeason.HistorySeasonVideoQualityOverride)){
                videoQuality = historySeason.HistorySeasonVideoQualityOverride;
            }
        }

        return (sublist, videoQuality);
    }


    private SeriesDataCache? cachedSeries;

    private async Task RefreshSeriesData(string seriesId, HistorySeries historySeries){
        if (cachedSeries == null || (!string.IsNullOrEmpty(cachedSeries.SeriesId) && cachedSeries.SeriesId != seriesId)){
            if (historySeries.SeriesType is SeriesType.Series or SeriesType.Movie){
                var seriesData = await crunInstance.CrSeries.SeriesById(seriesId, string.IsNullOrEmpty(crunInstance.CrunOptions.HistoryLang) ? crunInstance.DefaultLocale : crunInstance.CrunOptions.HistoryLang, true);
                if (seriesData is{ Data: not null }){
                    var firstEpisode = seriesData.Data.First();
                    cachedSeries = new SeriesDataCache{
                        SeriesDescription = firstEpisode.Description,
                        SeriesId = seriesId,
                        SeriesTitle = firstEpisode.Title,
                        ThumbnailImageUrl = GetSeriesThumbnail(seriesData),
                        HistorySeriesAvailableDubLang = Languages.SortListByLangList(firstEpisode.AudioLocales),
                        HistorySeriesAvailableSoftSubs = Languages.SortListByLangList(firstEpisode.SubtitleLocales)
                    };

                    historySeries.SeriesDescription = cachedSeries.SeriesDescription;
                    historySeries.ThumbnailImageUrl = cachedSeries.ThumbnailImageUrl;
                    historySeries.SeriesTitle = cachedSeries.SeriesTitle;
                    historySeries.HistorySeriesAvailableDubLang = cachedSeries.HistorySeriesAvailableDubLang;
                    historySeries.HistorySeriesAvailableSoftSubs = cachedSeries.HistorySeriesAvailableSoftSubs;
                }
            } else if (historySeries.SeriesType == SeriesType.Artist){
                var artisteData = await crunInstance.CrMusic.ParseArtistByIdAsync(seriesId, string.IsNullOrEmpty(crunInstance.CrunOptions.HistoryLang) ? crunInstance.DefaultLocale : crunInstance.CrunOptions.HistoryLang,
                    true);
                if (!string.IsNullOrEmpty(artisteData.Id)){
                    cachedSeries = new SeriesDataCache{
                        SeriesDescription = artisteData.Description ?? "",
                        SeriesId = artisteData.Id,
                        SeriesTitle = artisteData.Name ?? "",
                        ThumbnailImageUrl = artisteData.Images.PosterTall.FirstOrDefault(e => e.Height == 360)?.Source ?? "",
                        HistorySeriesAvailableDubLang = [],
                        HistorySeriesAvailableSoftSubs = []
                    };

                    historySeries.SeriesDescription = cachedSeries.SeriesDescription;
                    historySeries.ThumbnailImageUrl = cachedSeries.ThumbnailImageUrl;
                    historySeries.SeriesTitle = cachedSeries.SeriesTitle;
                    historySeries.HistorySeriesAvailableDubLang = cachedSeries.HistorySeriesAvailableDubLang;
                    historySeries.HistorySeriesAvailableSoftSubs = cachedSeries.HistorySeriesAvailableSoftSubs;
                }
            }
        } else{
            if (cachedSeries != null){
                historySeries.SeriesDescription = cachedSeries.SeriesDescription;
                historySeries.ThumbnailImageUrl = cachedSeries.ThumbnailImageUrl;
                historySeries.SeriesTitle = cachedSeries.SeriesTitle;
                historySeries.HistorySeriesAvailableDubLang = cachedSeries.HistorySeriesAvailableDubLang;
                historySeries.HistorySeriesAvailableSoftSubs = cachedSeries.HistorySeriesAvailableSoftSubs;
            }
        }
    }

    private void SortSeasons(HistorySeries series){
        var sortedSeasons = series.Seasons
            .OrderBy(s => {
                double seasonNum;
                return double.TryParse(s.SeasonNum, NumberStyles.Any, CultureInfo.InvariantCulture, out seasonNum)
                    ? seasonNum
                    : double.MaxValue;
            })
            .ToList();

        series.Seasons.Clear();

        foreach (var season in sortedSeasons){
            series.Seasons.Add(season);
        }
    }

    public void SortItems(){
        var currentSortingType = CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties?.SelectedSorting ?? SortingType.SeriesTitle;
        var sortingDir = CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties != null && CrunchyrollManager.Instance.CrunOptions.HistoryPageProperties.Ascending;
        DateTime today = DateTime.Now.Date;
        switch (currentSortingType){
            case SortingType.SeriesTitle:
                var sortedList = sortingDir
                    ? CrunchyrollManager.Instance.HistoryList.OrderByDescending(s => s.SeriesTitle).ToList()
                    : CrunchyrollManager.Instance.HistoryList.OrderBy(s => s.SeriesTitle).ToList();

                CrunchyrollManager.Instance.HistoryList.Clear();

                CrunchyrollManager.Instance.HistoryList.AddRange(sortedList);


                return;

            case SortingType.NextAirDate:

                var sortedSeriesDates = sortingDir
                    ? CrunchyrollManager.Instance.HistoryList
                        .OrderByDescending(s => {
                            var date = ParseDate(s.SonarrNextAirDate ?? string.Empty, today);
                            return date ?? DateTime.MinValue;
                        })
                        .ThenByDescending(s => s.SonarrNextAirDate == "Today" ? 1 : 0)
                        .ThenBy(s => string.IsNullOrEmpty(s.SonarrNextAirDate) ? 1 : 0)
                        .ThenBy(s => s.SeriesTitle)
                        .ToList()
                    : CrunchyrollManager.Instance.HistoryList
                        .OrderByDescending(s => s.SonarrNextAirDate == "Today")
                        .ThenBy(s => s.SonarrNextAirDate == "Today" ? s.SeriesTitle : null)
                        .ThenBy(s => {
                            var date = ParseDate(s.SonarrNextAirDate ?? string.Empty, today);
                            return date ?? DateTime.MaxValue;
                        })
                        .ThenBy(s => s.SeriesTitle)
                        .ToList();

                CrunchyrollManager.Instance.HistoryList.Clear();

                CrunchyrollManager.Instance.HistoryList.AddRange(sortedSeriesDates);


                return;

            case SortingType.HistorySeriesAddDate:

                var sortedSeriesAddDates = CrunchyrollManager.Instance.HistoryList
                    .OrderBy(s => sortingDir
                        ? -(s.HistorySeriesAddDate?.Date.Ticks ?? DateTime.MinValue.Ticks)
                        : s.HistorySeriesAddDate?.Date.Ticks ?? DateTime.MaxValue.Ticks)
                    .ThenBy(s => s.SeriesTitle)
                    .ToList();


                CrunchyrollManager.Instance.HistoryList.Clear();

                CrunchyrollManager.Instance.HistoryList.AddRange(sortedSeriesAddDates);

                return;
        }
    }

    public DateTime? ParseDate(string dateStr, DateTime today){
        if (dateStr == "Today"){
            return today;
        }

        if (DateTime.TryParseExact(dateStr, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)){
            return date;
        }

        return null;
    }

    private static SeriesType InferSeriesType(HistorySeries? historySeries){
        var seriesTypes = new List<SeriesType>();

        if (historySeries != null){
            seriesTypes.AddRange(historySeries.Seasons
                .SelectMany(season => season.EpisodesList)
                .Select(episode => episode.EpisodeSeriesType)
                .Where(type => type != SeriesType.Unknown));
        }

        if (seriesTypes.Count == 0){
            return historySeries?.SeriesType ?? SeriesType.Unknown;
        }

        if (seriesTypes.All(type => type == SeriesType.Artist)){
            return SeriesType.Artist;
        }

        if (seriesTypes.All(type => type == SeriesType.Movie)){
            return SeriesType.Movie;
        }

        return SeriesType.Series;
    }


    private string GetSeriesThumbnail(CrSeriesBase series){
        // var series = await crunInstance.CrSeries.SeriesById(seriesId);

        if (series.Data != null && series.Data.First().Images.PosterTall?.Count > 0){
            var imagesPosterTall = series.Data.First().Images.PosterTall;
            if (imagesPosterTall != null) return imagesPosterTall.First().First(e => e.Height == 360).Source;
        }

        return "";
    }

    private void RemoveUnavailableEpisodes(HistorySeries historySeries){
        if (!crunInstance.CrunOptions.HistoryRemoveMissingEpisodes){
            return;
        }

        var seasonsToRemove = new List<HistorySeason>();

        foreach (var season in historySeries.Seasons){
            var unavailableEpisodes = season.EpisodesList
                .Where(episode => !episode.IsEpisodeAvailableOnStreamingService)
                .ToList();

            foreach (var episode in unavailableEpisodes){
                season.EpisodesList.Remove(episode);
            }

            if (season.EpisodesList.Count == 0){
                seasonsToRemove.Add(season);
                continue;
            }

            season.EpisodesList.Sort(new NumericStringPropertyComparer());
            season.UpdateDownloaded();
        }

        foreach (var season in seasonsToRemove){
            historySeries.Seasons.Remove(season);
        }

        historySeries.UpdateNewEpisodes();
        SortSeasons(historySeries);
    }


    private HistorySeason NewHistorySeason(List<IHistorySource> episodeList, IHistorySource firstEpisode){
        var newSeason = new HistorySeason{
            SeasonTitle = firstEpisode.GetSeasonTitle(),
            SeasonId = firstEpisode.GetSeasonId(),
            SeasonNum = firstEpisode.GetSeasonNum(),
            EpisodesList = [],
            SpecialSeason = firstEpisode.IsSpecialSeason()
        };

        foreach (var historySource in episodeList){
            if (historySource.GetSeasonId() != newSeason.SeasonId){
                continue;
            }

            var newHistoryEpisode = new HistoryEpisode{
                EpisodeTitle = historySource.GetEpisodeTitle(),
                EpisodeDescription = historySource.GetEpisodeDescription(),
                EpisodeId = historySource.GetEpisodeId(),
                Episode = historySource.GetEpisodeNumber(),
                EpisodeSeasonNum = historySource.GetSeasonNum(),
                SpecialEpisode = historySource.IsSpecialEpisode(),
                HistoryEpisodeAvailableDubLang = historySource.GetEpisodeAvailableDubLang(),
                HistoryEpisodeAvailableSoftSubs = historySource.GetEpisodeAvailableSoftSubs(),
                EpisodeCrPremiumAirDate = historySource.GetAvailableDate(),
                EpisodeType = historySource.GetEpisodeType(),
                EpisodeSeriesType = historySource.GetSeriesType(),
                IsEpisodeAvailableOnStreamingService = true,
                ThumbnailImageUrl = historySource.GetImageUrl(),
            };

            newSeason.EpisodesList.Add(newHistoryEpisode);
        }

        return newSeason;
    }

    public void MatchHistorySeriesWithSonarr(bool updateAll){
        if (crunInstance.CrunOptions.SonarrProperties is{ SonarrEnabled: false }){
            return;
        }

        foreach (var historySeries in crunInstance.HistoryList){
            if (string.IsNullOrEmpty(historySeries.SonarrSeriesId)){
                var sonarrSeries = FindClosestMatch(historySeries.SeriesTitle ?? string.Empty);
                if (sonarrSeries != null){
                    historySeries.SonarrSeriesId = sonarrSeries.Id + "";
                    historySeries.SonarrTvDbId = sonarrSeries.TvdbId + "";
                    historySeries.SonarrSlugTitle = sonarrSeries.TitleSlug;
                }
            } else if (updateAll){
                var sonarrSeries = SonarrClient.Instance.SonarrSeries.FirstOrDefault(series => series.Id + "" == historySeries.SonarrSeriesId);
                if (sonarrSeries != null){
                    historySeries.SonarrSeriesId = sonarrSeries.Id + "";
                    historySeries.SonarrTvDbId = sonarrSeries.TvdbId + "";
                    historySeries.SonarrSlugTitle = sonarrSeries.TitleSlug;
                } else{
                    Console.Error.WriteLine($"Unable to find sonarr series for {historySeries.SeriesTitle}");
                }
            }
        }
    }

    private static readonly object _lock = new object();

    public async Task MatchHistoryEpisodesWithSonarr(bool rematchAll, HistorySeries historySeries){
        if (crunInstance.CrunOptions.SonarrProperties is{ SonarrEnabled: false }){
            return;
        }

        if (!string.IsNullOrEmpty(historySeries.SonarrSeriesId)){
            List<SonarrEpisode> episodes = await SonarrClient.Instance.GetEpisodes(int.Parse(historySeries.SonarrSeriesId));

            historySeries.SonarrNextAirDate = GetNextAirDate(episodes);

            List<HistoryEpisode> allHistoryEpisodes = [];

            foreach (var historySeriesSeason in historySeries.Seasons){
                allHistoryEpisodes.AddRange(historySeriesSeason.EpisodesList);
            }

            if (!rematchAll){
                var historyEpisodesWithSonarrIds = allHistoryEpisodes
                    .Where(e => !string.IsNullOrEmpty(e.SonarrEpisodeId))
                    .ToList();

                Parallel.ForEach(historyEpisodesWithSonarrIds, historyEpisode => {
                    var sonarrEpisode = episodes.FirstOrDefault(e => e.Id.ToString().Equals(historyEpisode.SonarrEpisodeId));

                    if (sonarrEpisode != null){
                        historyEpisode.AssignSonarrEpisodeData(sonarrEpisode);
                    }
                });

                var historyEpisodeIds = new HashSet<string>(historyEpisodesWithSonarrIds.Select(e => e.SonarrEpisodeId!));

                episodes.RemoveAll(e => historyEpisodeIds.Contains(e.Id.ToString()));

                allHistoryEpisodes = allHistoryEpisodes
                    .Where(e => string.IsNullOrEmpty(e.SonarrEpisodeId))
                    .ToList();
            }

            List<HistoryEpisode> failedEpisodes = [];

            Parallel.ForEach(allHistoryEpisodes, historyEpisode => {
                if (string.IsNullOrEmpty(historyEpisode.SonarrEpisodeId)){
                    // Create a copy of the episodes list for each thread
                    var episodesCopy = new List<SonarrEpisode>(episodes);

                    var episode = FindClosestMatchEpisodes(episodesCopy, historyEpisode.EpisodeTitle ?? string.Empty);
                    if (episode != null){
                        historyEpisode.AssignSonarrEpisodeData(episode);
                        lock (_lock){
                            episodes.Remove(episode);
                        }
                    } else{
                        lock (_lock){
                            failedEpisodes.Add(historyEpisode);
                        }
                    }
                }
            });

            Parallel.ForEach(failedEpisodes, historyEpisode => {
                var episode = episodes.Find(ele => {
                    if (ele == null){
                        return false;
                    }

                    var episodeNumberStr = ele.EpisodeNumber.ToString();
                    var seasonNumberStr = ele.SeasonNumber.ToString();

                    return episodeNumberStr == historyEpisode.Episode && seasonNumberStr == historyEpisode.EpisodeSeasonNum;
                });
                if (episode != null){
                    historyEpisode.AssignSonarrEpisodeData(episode);

                    lock (_lock){
                        episodes.Remove(episode);
                    }
                } else{
                    var episode1 = episodes.Find(ele => {
                        if (ele == null){
                            return false;
                        }

                        return !string.IsNullOrEmpty(historyEpisode.EpisodeDescription) && !string.IsNullOrEmpty(ele.Overview) && Helpers.CalculateCosineSimilarity(ele.Overview, historyEpisode.EpisodeDescription) > 0.8;
                    });

                    if (episode1 != null){
                        historyEpisode.AssignSonarrEpisodeData(episode1);

                        lock (_lock){
                            episodes.Remove(episode1);
                        }
                    } else{
                        var episode2 = episodes.Find(ele => {
                            if (ele == null){
                                return false;
                            }

                            return ele.AbsoluteEpisodeNumber + "" == historyEpisode.Episode;
                        });
                        if (episode2 != null){
                            historyEpisode.AssignSonarrEpisodeData(episode2);

                            lock (_lock){
                                episodes.Remove(episode2);
                            }
                        } else{
                            Console.Error.WriteLine($"Could not match episode {historyEpisode.EpisodeTitle} to sonarr episode");
                        }
                    }
                }
            });
        }
    }

    public string GetNextAirDate(List<SonarrEpisode> episodes){
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
        if (string.IsNullOrEmpty(title)){
            return null;
        }

        SonarrSeries? closestMatch = null;
        double highestSimilarity = 0.0;

        Parallel.ForEach(SonarrClient.Instance.SonarrSeries, series => {
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
            if (episode != null){
                double similarity = CalculateSimilarity(episode.Title, title);
                lock (lockObject) // Ensure thread-safe access to shared variables
                {
                    if (similarity > highestSimilarity){
                        highestSimilarity = similarity;
                        closestMatch = episode;
                    }
                }
            }
        });

        return highestSimilarity < 0.8 ? null : closestMatch;
    }

    public CrBrowseSeries? FindClosestMatchCrSeries(List<CrBrowseSeries> episodeList, string title){
        CrBrowseSeries? closestMatch = null;
        double highestSimilarity = 0.0;
        object lockObject = new object(); // To synchronize access to shared variables

        Parallel.ForEach(episodeList, episode => {
            if (episode != null){
                double similarity = CalculateSimilarity(episode.Title ?? string.Empty, title);
                lock (lockObject) // Ensure thread-safe access to shared variables
                {
                    if (similarity > highestSimilarity){
                        highestSimilarity = similarity;
                        closestMatch = episode;
                    }
                }
            }
        });

        return highestSimilarity < 0.8 ? null : closestMatch;
    }

    public double CalculateSimilarity(string source, string target){
        int distance = LevenshteinDistance(source, target);
        return 1.0 - (double)distance / Math.Max(source.Length, target.Length);
    }


    private int LevenshteinDistance(string source, string target){
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
    public int Compare(HistoryEpisode? x, HistoryEpisode? y){
        if (double.TryParse(x?.Episode, NumberStyles.Any, CultureInfo.InvariantCulture, out double xDouble) &&
            double.TryParse(y?.Episode, NumberStyles.Any, CultureInfo.InvariantCulture, out double yDouble)){
            return xDouble.CompareTo(yDouble);
        }

        // Fall back to string comparison if not parseable as doubles
        return string.Compare(x?.Episode, y?.Episode, StringComparison.Ordinal);
    }
}
