using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Views;
using Newtonsoft.Json;
using ReactiveUI;

namespace CRD.ViewModels;

public partial class UpcomingPageViewModel : ViewModelBase{
    #region Query

    private string query = @"query (
                $season: MediaSeason,
                $year: Int,
                $format: MediaFormat,
                $excludeFormat: MediaFormat,
                $status: MediaStatus,
                $minEpisodes: Int,
                $page: Int,
            ){
                Page(page: $page) {
                    pageInfo {
                        hasNextPage
                        total
                    }
                    media(
                        season: $season
                        seasonYear: $year
                        format: $format,
                        format_not: $excludeFormat,
                        status: $status,
                        episodes_greater: $minEpisodes,
                        isAdult: false,
                        type: ANIME,
                        sort: TITLE_ENGLISH,
                    ) {
                        id
                        idMal
                        title {
                            romaji
                            native
                            english
                        }
                        startDate {
                            year
                            month
                            day
                        }
                        endDate {
                            year
                            month
                            day
                        }
                        status
                        season
                        format
                        genres
                        synonyms
                        duration
                        popularity
                        episodes
                        source(version: 2)
                        countryOfOrigin
                        hashtag
                        averageScore
                        siteUrl
                        description
                        bannerImage
                        isAdult
                        coverImage {
                            extraLarge
                            color
                        }
                        trailer {
                            id
                            site
                            thumbnail
                        }
                        externalLinks {
                            site
                            icon
                            color
                            url
                        }
                        rankings {
                            rank
                            type
                            season
                            allTime
                        }
                        studios(isMain: true) {
                            nodes {
                                id
                                name
                                siteUrl
                            }
                        }
                        relations {
                            edges {
                                relationType(version: 2)
                                node {
                                    id
                                    title {
                                        romaji
                                        native
                                        english
                                    }
                                    siteUrl
                                }
                            }
                        }
                        airingSchedule(
                            notYetAired: true
                            perPage: 2
                        ) {
                            nodes {
                                episode
                                airingAt
                            }
                        }
                    }
                }
            }";

    #endregion

    [ObservableProperty]
    private AnilistSeries? _selectedSeries;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private bool _quickAddMode;
    
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private SortingListElement? _selectedSorting;

    [ObservableProperty]
    private static bool _sortingSelectionOpen;

    private SortingType currentSortingType;

    [ObservableProperty]
    private static bool _sortDir;

    public ObservableCollection<SortingListElement> SortingList{ get; } =[];

    public ObservableCollection<SeasonViewModel> Seasons{ get; set; } =[];

    public ObservableCollection<AnilistSeries> SelectedSeason{ get; set; } =[];

    private SeasonViewModel currentSelection;

    public UpcomingPageViewModel(){
        LoadSeasons();
    }

    private async void LoadSeasons(){
        SeasonsPageProperties? properties = CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties;

        currentSortingType = properties?.SelectedSorting ?? SortingType.SeriesTitle;
        SortDir = properties?.Ascending ?? false;

        foreach (SortingType sortingType in Enum.GetValues(typeof(SortingType))){
            if (sortingType == SortingType.HistorySeriesAddDate){
                continue;
            }

            var combobox = new SortingListElement(){ SortingTitle = sortingType.GetEnumMemberValue(), SelectedSorting = sortingType };
            SortingList.Add(combobox);
            if (sortingType == currentSortingType){
                SelectedSorting = combobox;
            }
        }

        Seasons = GetTargetSeasonsAndYears();

        currentSelection = Seasons.Last();
        currentSelection.IsSelected = true;

        var list = await GetSeriesForSeason(currentSelection.Season, currentSelection.Year, false);
        SelectedSeason.Clear();
        
        var crunchySimul = await CrunchyrollManager.Instance.CrSeries.GetSeasonalSeries(currentSelection.Season, currentSelection.Year + "", "");  
        
        foreach (var anilistSeries in list){
            SelectedSeason.Add(anilistSeries);
            if (!string.IsNullOrEmpty(anilistSeries.CrunchyrollID) && crunchySimul?.Data is{ Count: > 0 }){
                var crunchySeries = crunchySimul.Data.FirstOrDefault(ele => ele.Id == anilistSeries.CrunchyrollID);
                if (crunchySeries != null){
                    anilistSeries.AudioLocales.AddRange(Languages.LocalListToLangList(crunchySeries.SeriesMetadata.AudioLocales ??[]));
                    anilistSeries.SubtitleLocales.AddRange(Languages.LocalListToLangList(crunchySeries.SeriesMetadata.SubtitleLocales ??[]));
                }
            }
        }

        SortItems();
    }

    [RelayCommand]
    public async Task SelectSeasonCommand(SeasonViewModel selectedSeason){
        currentSelection.IsSelected = false;
        currentSelection = selectedSeason;
        currentSelection.IsSelected = true;

        var list = await GetSeriesForSeason(currentSelection.Season, currentSelection.Year, false);
        SelectedSeason.Clear();
        
        var crunchySimul = await CrunchyrollManager.Instance.CrSeries.GetSeasonalSeries(currentSelection.Season, currentSelection.Year + "", "");  
        
        foreach (var anilistSeries in list){
            SelectedSeason.Add(anilistSeries);
            if (!string.IsNullOrEmpty(anilistSeries.CrunchyrollID) && crunchySimul?.Data is{ Count: > 0 }){
                var crunchySeries = crunchySimul.Data.FirstOrDefault(ele => ele.Id == anilistSeries.CrunchyrollID);
                if (crunchySeries != null){
                    anilistSeries.AudioLocales.AddRange(Languages.LocalListToLangList(crunchySeries.SeriesMetadata.AudioLocales ??[]));
                    anilistSeries.SubtitleLocales.AddRange(Languages.LocalListToLangList(crunchySeries.SeriesMetadata.SubtitleLocales ??[]));
                }
            }
        }
        SortItems();
    }

    [RelayCommand]
    public void OpenTrailer(AnilistSeries series){
        if (series.Trailer.Site.Equals("youtube")){
            var url = "https://www.youtube.com/watch?v=" + series.Trailer.Id; // Replace with your video URL
            Process.Start(new ProcessStartInfo{
                FileName = url,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    public async Task AddToHistory(AnilistSeries series){
        if (ProgramManager.Instance.FetchingData){
            MessageBus.Current.SendMessage(new ToastMessage($"History still loading", ToastType.Warning, 3));
            return;
        }
        
        if (!string.IsNullOrEmpty(series.CrunchyrollID)){
            if (CrunchyrollManager.Instance.CrunOptions.History){
                series.IsInHistory = true;
                RaisePropertyChanged(nameof(series.IsInHistory));
                var sucess = await CrunchyrollManager.Instance.History.CrUpdateSeries(series.CrunchyrollID, "");
                series.IsInHistory = sucess;
                RaisePropertyChanged(nameof(series.IsInHistory));

                if (sucess){
                    MessageBus.Current.SendMessage(new ToastMessage($"Series added to History", ToastType.Information, 3));
                } else{
                    MessageBus.Current.SendMessage(new ToastMessage($"Series couldn't get added to History\n(maybe not available in your region)", ToastType.Error, 3));
                }
            } else{
                MessageBus.Current.SendMessage(new ToastMessage($"Series couldn't get added to History", ToastType.Error, 3));
            }
        } else{
            MessageBus.Current.SendMessage(new ToastMessage($"Series couldn't get added to History", ToastType.Error, 3));
        }
    }

    private async Task<List<AnilistSeries>> GetSeriesForSeason(string season, int year, bool forceRefresh){
        if (ProgramManager.Instance.AnilistSeasons.ContainsKey(season + year) && !forceRefresh){
            return ProgramManager.Instance.AnilistSeasons[season + year];
        }

        IsLoading = true;

        var variables = new{
            season,
            year,
            format = "TV",
            page = 1
        };

        var payload = new{
            query,
            variables
        };

        string jsonPayload = JsonConvert.SerializeObject(payload, Formatting.Indented);

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrls.Anilist);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            Console.Error.WriteLine($"Anilist Request Failed for {season} {year}");
            return[];
        }

        AniListResponse aniListResponse = Helpers.Deserialize<AniListResponse>(response.ResponseContent, CrunchyrollManager.Instance.SettingsJsonSerializerSettings) ?? new AniListResponse();

        var list = aniListResponse.Data?.Page?.Media ??[];

        list = list.Where(ele => ele.ExternalLinks != null && ele.ExternalLinks.Any(external =>
            string.Equals(external.Site, "Crunchyroll", StringComparison.OrdinalIgnoreCase))).ToList();


        foreach (var anilistEle in list){
            anilistEle.ThumbnailImage = await Helpers.LoadImage(anilistEle.CoverImage.ExtraLarge, 185, 265);
            anilistEle.Description = anilistEle.Description
                .Replace("<i>", "")
                .Replace("</i>", "")
                .Replace("<BR>", "")
                .Replace("<br>", "");


            if (anilistEle.ExternalLinks != null){
                var url = anilistEle.ExternalLinks.First(external =>
                    string.Equals(external.Site, "Crunchyroll", StringComparison.OrdinalIgnoreCase)).Url;

                string pattern = @"series\/([^\/]+)";

                Match match = Regex.Match(url, pattern);
                if (match.Success){
                    anilistEle.CrunchyrollID = match.Groups[1].Value;
                    anilistEle.HasCrID = true;

                    if (CrunchyrollManager.Instance.CrunOptions.History){
                        var historyIDs = new HashSet<string>(CrunchyrollManager.Instance.HistoryList.Select(item => item.SeriesId ?? ""));

                        if (historyIDs.Contains(anilistEle.CrunchyrollID)){
                            anilistEle.IsInHistory = true;
                        }
                    }
                } else{
                    Uri uri = new Uri(url);

                    if (uri.Host == "www.crunchyroll.com"
                        && uri.AbsolutePath != "/"
                        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)){
                        HttpRequestMessage getUrlRequest = new HttpRequestMessage(HttpMethod.Head, url);

                        string? finalUrl = "";

                        try{
                            HttpResponseMessage getUrlResponse = await HttpClientReq.Instance.GetHttpClient().SendAsync(getUrlRequest);

                            finalUrl = getUrlResponse.RequestMessage?.RequestUri?.ToString();
                        } catch (Exception ex){
                            Console.WriteLine($"Error: {ex.Message}");
                        }

                        Match match2 = Regex.Match(finalUrl ?? string.Empty, pattern);
                        if (match2.Success){
                            anilistEle.CrunchyrollID = match2.Groups[1].Value;
                            anilistEle.HasCrID = true;

                            if (CrunchyrollManager.Instance.CrunOptions.History){
                                var historyIDs = new HashSet<string>(CrunchyrollManager.Instance.HistoryList.Select(item => item.SeriesId ?? ""));

                                if (historyIDs.Contains(anilistEle.CrunchyrollID)){
                                    anilistEle.IsInHistory = true;
                                }
                            }
                        } else{
                            anilistEle.CrunchyrollID = "";
                            anilistEle.HasCrID = false;
                        }
                    } else{
                        anilistEle.CrunchyrollID = "";
                        anilistEle.HasCrID = false;
                    }
                }
            }
        }


        ProgramManager.Instance.AnilistSeasons[season + year] = list;

        IsLoading = false;

        return list;
    }

    private ObservableCollection<SeasonViewModel> GetTargetSeasonsAndYears(){
        DateTime now = DateTime.Now;
        int currentMonth = now.Month;
        int currentYear = now.Year;

        string currentSeason;
        if (currentMonth >= 1 && currentMonth <= 3)
            currentSeason = "WINTER";
        else if (currentMonth >= 4 && currentMonth <= 6)
            currentSeason = "SPRING";
        else if (currentMonth >= 7 && currentMonth <= 9)
            currentSeason = "SUMMER";
        else
            currentSeason = "FALL";


        var seasons = new List<string>{ "WINTER", "SPRING", "SUMMER", "FALL" };

        int currentSeasonIndex = seasons.IndexOf(currentSeason);

        var targetSeasons = new ObservableCollection<SeasonViewModel>();

        // Includes: -2 (two seasons ago), -1 (previous), 0 (current), 1 (next)
        for (int i = -2; i <= 1; i++){
            int targetIndex = (currentSeasonIndex + i + 4) % 4;
            string targetSeason = seasons[targetIndex];
            int targetYear = currentYear;


            if (i < 0 && targetIndex == 3){
                targetYear--;
            } else if (i > 0 && targetIndex == 0){
                targetYear++;
            }


            targetSeasons.Add(new SeasonViewModel(){ Season = targetSeason, Year = targetYear });
        }

        return targetSeasons;
    }

    public void SelectionChangedOfSeries(AnilistSeries? value){
        if (value != null && !QuickAddMode) value.IsExpanded = !value.IsExpanded;
        SelectedSeries = null;
        SelectedIndex = -1;
    }

    partial void OnSelectedSeriesChanged(AnilistSeries? value){
        SelectionChangedOfSeries(value);
    }

    #region Sorting

    private void UpdateSettings(){
        if (CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties != null){
            CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties.SelectedSorting = currentSortingType;
            CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties.Ascending = SortDir;
        } else{
            CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties = new SeasonsPageProperties(){ SelectedSorting = currentSortingType, Ascending = SortDir };
        }

        CfgManager.WriteCrSettings();
    }

    partial void OnSelectedSortingChanged(SortingListElement? oldValue, SortingListElement? newValue){
        if (newValue == null){
            if (CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties != null){
                CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties.Ascending = !CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties.Ascending;
                SortDir = CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties.Ascending;
            }

            Dispatcher.UIThread.InvokeAsync(() => {
                SelectedSorting = oldValue ?? SortingList.First();
                RaisePropertyChanged(nameof(SelectedSorting));
            });
            return;
        }

        currentSortingType = newValue.SelectedSorting;
        if (CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties != null) CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties.SelectedSorting = currentSortingType;
        SortItems();

        SortingSelectionOpen = false;
        UpdateSettings();
    }

    private void SortItems(){
        var sortingDir = CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties != null && CrunchyrollManager.Instance.CrunOptions.SeasonsPageProperties.Ascending;

        var sortedList = currentSortingType switch{
            SortingType.SeriesTitle => sortingDir
                ? SelectedSeason
                    .OrderByDescending(item => item.Title.English)
                    .ToList()
                : SelectedSeason
                    .OrderBy(item => item.Title.English)
                    .ToList(),
            SortingType.NextAirDate => sortingDir
                ? SelectedSeason
                    .OrderByDescending(item => item.StartDate?.ToDateTime() ?? DateTime.MinValue)
                    .ThenByDescending(item => item.Title.English)
                    .ToList()
                : SelectedSeason
                    .OrderBy(item => item.StartDate?.ToDateTime() ?? DateTime.MinValue)
                    .ThenBy(item => item.Title.English)
                    .ToList(),
            _ => SelectedSeason.ToList()
        };


        SelectedSeason.Clear();
        foreach (var item in sortedList){
            SelectedSeason.Add(item);
        }
    }

    #endregion
}