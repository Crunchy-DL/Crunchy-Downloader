using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;
using CRD.Views;
using HtmlAgilityPack;
using Newtonsoft.Json;
using ReactiveUI;

namespace CRD.Downloader;

public class CalendarManager{
    #region Calendar Variables

    private Dictionary<string, CalendarWeek> calendar = new();

    private Dictionary<string, string> calendarLanguage = new(){
        { "en-us", "https://www.crunchyroll.com/simulcastcalendar" },
        { "es", "https://www.crunchyroll.com/es/simulcastcalendar" },
        { "es-es", "https://www.crunchyroll.com/es-es/simulcastcalendar" },
        { "pt-br", "https://www.crunchyroll.com/pt-br/simulcastcalendar" },
        { "pt-pt", "https://www.crunchyroll.com/pt-pt/simulcastcalendar" },
        { "fr", "https://www.crunchyroll.com/fr/simulcastcalendar" },
        { "de", "https://www.crunchyroll.com/de/simulcastcalendar" },
        { "ar", "https://www.crunchyroll.com/ar/simulcastcalendar" },
        { "it", "https://www.crunchyroll.com/it/simulcastcalendar" },
        { "ru", "https://www.crunchyroll.com/ru/simulcastcalendar" },
        { "hi", "https://www.crunchyroll.com/hi/simulcastcalendar" },
    };

    #endregion


    #region Singelton

    private static CalendarManager? _instance;
    private static readonly object Padlock = new();

    public static CalendarManager Instance{
        get{
            if (_instance == null){
                lock (Padlock){
                    if (_instance == null){
                        _instance = new CalendarManager();
                    }
                }
            }

            return _instance;
        }
    }

    #endregion


    public async Task<CalendarWeek> GetCalendarForDate(string weeksMondayDate, bool forceUpdate){
        if (!forceUpdate && calendar.TryGetValue(weeksMondayDate, out var forDate)){
            return forDate;
        }

        var request = calendarLanguage.ContainsKey(CrunchyrollManager.Instance.CrunOptions.SelectedCalendarLanguage ?? "en-us")
            ? HttpClientReq.CreateRequestMessage($"{calendarLanguage[CrunchyrollManager.Instance.CrunOptions.SelectedCalendarLanguage ?? "en-us"]}?filter=premium&date={weeksMondayDate}", HttpMethod.Get, false, false, null)
            : HttpClientReq.CreateRequestMessage($"{calendarLanguage["en-us"]}?filter=premium&date={weeksMondayDate}", HttpMethod.Get, false, false, null);


        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (!response.IsOk){
            if (response.ResponseContent.Contains("<title>Just a moment...</title>") || 
                response.ResponseContent.Contains("<title>Access denied</title>") || 
                response.ResponseContent.Contains("<title>Attention Required! | Cloudflare</title>") || 
                response.ResponseContent.Trim().Equals("error code: 1020") || 
                response.ResponseContent.IndexOf("<title>DDOS-GUARD</title>", StringComparison.OrdinalIgnoreCase) > -1){
                MessageBus.Current.SendMessage(new ToastMessage("Blocked by Cloudflare. Use the custom calendar.", ToastType.Error, 5));
                Console.Error.WriteLine($"Blocked by Cloudflare. Use the custom calendar.");
            } else{
                Console.Error.WriteLine($"Calendar request failed");
            }
            return new CalendarWeek();
        }

        CalendarWeek week = new CalendarWeek();
        week.CalendarDays = new List<CalendarDay>();

        // Load the HTML content from a file
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(WebUtility.HtmlDecode(response.ResponseContent));

        // Select each 'li' element with class 'day'
        var dayNodes = doc.DocumentNode.SelectNodes("//li[contains(@class, 'day')]");

        if (dayNodes != null){
            foreach (var day in dayNodes){
                // Extract the date and day name
                var date = day.SelectSingleNode(".//time[@datetime]")?.GetAttributeValue("datetime", "No date");
                if (date != null){
                    DateTime dayDateTime = DateTime.Parse(date, null, DateTimeStyles.RoundtripKind);

                    if (week.FirstDayOfWeek == DateTime.MinValue){
                        week.FirstDayOfWeek = dayDateTime;
                        week.FirstDayOfWeekString = dayDateTime.ToString("yyyy-MM-dd");
                    }

                    var dayName = day.SelectSingleNode(".//h1[@class='day-name']/time")?.InnerText.Trim();

                    CalendarDay calDay = new CalendarDay();

                    calDay.CalendarEpisodes = new List<CalendarEpisode>();
                    calDay.DayName = dayName;
                    calDay.DateTime = dayDateTime;

                    // Iterate through each episode listed under this day
                    var episodes = day.SelectNodes(".//article[contains(@class, 'release')]");
                    if (episodes != null){
                        foreach (var episode in episodes){
                            var episodeTimeStr = episode.SelectSingleNode(".//time[contains(@class, 'available-time')]")?.GetAttributeValue("datetime", null);
                            if (episodeTimeStr != null){
                                DateTime episodeTime = DateTime.Parse(episodeTimeStr, null, DateTimeStyles.RoundtripKind);
                                var hasPassed = DateTime.Now > episodeTime;

                                var episodeName = episode.SelectSingleNode(".//h1[contains(@class, 'episode-name')]")?.SelectSingleNode(".//cite[@itemprop='name']")?.InnerText.Trim();
                                var seasonLink = episode.SelectSingleNode(".//a[contains(@class, 'js-season-name-link')]")?.GetAttributeValue("href", "No link");
                                var episodeLink = episode.SelectSingleNode(".//a[contains(@class, 'available-episode-link')]")?.GetAttributeValue("href", "No link");
                                var thumbnailUrl = episode.SelectSingleNode(".//img[contains(@class, 'thumbnail')]")?.GetAttributeValue("src", "No image");
                                var isPremiumOnly = episode.SelectSingleNode(".//svg[contains(@class, 'premium-flag')]") != null;
                                var isPremiere = episode.SelectSingleNode(".//div[contains(@class, 'premiere-flag')]") != null;
                                var seasonName = episode.SelectSingleNode(".//a[contains(@class, 'js-season-name-link')]")?.SelectSingleNode(".//cite[@itemprop='name']")?.InnerText.Trim();
                                var episodeNumber = episode.SelectSingleNode(".//meta[contains(@itemprop, 'episodeNumber')]")?.GetAttributeValue("content", "?");

                                CalendarEpisode calEpisode = new CalendarEpisode();

                                calEpisode.DateTime = episodeTime;
                                calEpisode.HasPassed = hasPassed;
                                calEpisode.EpisodeName = episodeName;
                                calEpisode.SeriesUrl = seasonLink;
                                calEpisode.EpisodeUrl = episodeLink;
                                calEpisode.ThumbnailUrl = thumbnailUrl;
                                calEpisode.IsPremiumOnly = isPremiumOnly;
                                calEpisode.IsPremiere = isPremiere;
                                calEpisode.SeasonName = seasonName;
                                calEpisode.EpisodeNumber = episodeNumber;

                                calDay.CalendarEpisodes.Add(calEpisode);
                            }
                        }
                    }

                    week.CalendarDays.Add(calDay);
                }
            }
        } else{
            Console.Error.WriteLine("No days found in the HTML document.");
        }

        calendar[weeksMondayDate] = week;


        return week;
    }


    public async Task<CalendarWeek> BuildCustomCalendar(DateTime calTargetDate, bool forceUpdate){
        if (CrunchyrollManager.Instance.CrunOptions.CalendarShowUpcomingEpisodes){
            await LoadAnilistUpcoming();
        }

        if (!forceUpdate && calendar.TryGetValue("C" + calTargetDate.ToString("yyyy-MM-dd"), out var forDate)){
            return forDate;
        }


        CalendarWeek week = new CalendarWeek();
        week.CalendarDays = new List<CalendarDay>();

        DateTime targetDay = calTargetDate;

        for (int i = 0; i < 7; i++){
            CalendarDay calDay = new CalendarDay();

            calDay.CalendarEpisodes = new List<CalendarEpisode>();
            calDay.DateTime = targetDay.AddDays(-i);
            calDay.DayName = calDay.DateTime.DayOfWeek.ToString();

            week.CalendarDays.Add(calDay);
        }

        week.CalendarDays.Reverse();

        var firstDayOfWeek = week.CalendarDays.First().DateTime;
        week.FirstDayOfWeek = firstDayOfWeek;

        var newEpisodesBase = await CrunchyrollManager.Instance.CrEpisode.GetNewEpisodes("", 200, firstDayOfWeek, true);

        if (newEpisodesBase is{ Data.Count: > 0 }){
            var newEpisodes = newEpisodesBase.Data;

            //EpisodeAirDate
            foreach (var crBrowseEpisode in newEpisodes){
                DateTime episodeAirDate = crBrowseEpisode.EpisodeMetadata.EpisodeAirDate.Kind == DateTimeKind.Utc
                    ? crBrowseEpisode.EpisodeMetadata.EpisodeAirDate.ToLocalTime()
                    : crBrowseEpisode.EpisodeMetadata.EpisodeAirDate;

                DateTime premiumAvailableStart = crBrowseEpisode.EpisodeMetadata.PremiumAvailableDate.Kind == DateTimeKind.Utc
                    ? crBrowseEpisode.EpisodeMetadata.PremiumAvailableDate.ToLocalTime()
                    : crBrowseEpisode.EpisodeMetadata.PremiumAvailableDate;

                DateTime now = DateTime.Now;
                DateTime oneYearFromNow = now.AddYears(1);

                DateTime targetDate;

                if (CrunchyrollManager.Instance.CrunOptions.CalendarFilterByAirDate){
                    targetDate = episodeAirDate;

                    if (targetDate >= oneYearFromNow){
                        DateTime freeAvailableStart = crBrowseEpisode.EpisodeMetadata.FreeAvailableDate.Kind == DateTimeKind.Utc
                            ? crBrowseEpisode.EpisodeMetadata.FreeAvailableDate.ToLocalTime()
                            : crBrowseEpisode.EpisodeMetadata.FreeAvailableDate;

                        if (freeAvailableStart <= oneYearFromNow){
                            targetDate = freeAvailableStart;
                        } else{
                            targetDate = premiumAvailableStart;
                        }
                    }
                } else{
                    targetDate = premiumAvailableStart;

                    if (targetDate >= oneYearFromNow){
                        DateTime freeAvailableStart = crBrowseEpisode.EpisodeMetadata.FreeAvailableDate.Kind == DateTimeKind.Utc
                            ? crBrowseEpisode.EpisodeMetadata.FreeAvailableDate.ToLocalTime()
                            : crBrowseEpisode.EpisodeMetadata.FreeAvailableDate;

                        if (freeAvailableStart <= oneYearFromNow){
                            targetDate = freeAvailableStart;
                        } else{
                            targetDate = episodeAirDate;
                        }
                    }
                }

                var dubFilter = CrunchyrollManager.Instance.CrunOptions.CalendarDubFilter;

                if (CrunchyrollManager.Instance.CrunOptions.CalendarHideDubs && crBrowseEpisode.EpisodeMetadata.SeasonTitle != null &&
                    (crBrowseEpisode.EpisodeMetadata.SeasonTitle.EndsWith("Dub)") || crBrowseEpisode.EpisodeMetadata.SeasonTitle.EndsWith("Audio)")) &&
                    (string.IsNullOrEmpty(dubFilter) || dubFilter == "none" || (crBrowseEpisode.EpisodeMetadata.AudioLocale != null && crBrowseEpisode.EpisodeMetadata.AudioLocale.GetEnumMemberValue() != dubFilter))){
                    //|| crBrowseEpisode.EpisodeMetadata.AudioLocale != Locale.JaJp
                    continue;
                }


                if (!string.IsNullOrEmpty(dubFilter) && dubFilter != "none"){
                    if (crBrowseEpisode.EpisodeMetadata.AudioLocale != null && crBrowseEpisode.EpisodeMetadata.AudioLocale.GetEnumMemberValue() != dubFilter){
                        continue;
                    }
                }

                var calendarDay = (from day in week.CalendarDays
                    where day.DateTime != DateTime.MinValue && day.DateTime.Date == targetDate.Date
                    select day).FirstOrDefault();

                if (calendarDay != null){
                    CalendarEpisode calEpisode = new CalendarEpisode();

                    calEpisode.DateTime = targetDate;
                    calEpisode.HasPassed = DateTime.Now > targetDate;
                    calEpisode.EpisodeName = crBrowseEpisode.Title;
                    calEpisode.SeriesUrl = $"https://www.crunchyroll.com/{CrunchyrollManager.Instance.CrunOptions.HistoryLang}/series/" + crBrowseEpisode.EpisodeMetadata.SeriesId;
                    calEpisode.EpisodeUrl = $"https://www.crunchyroll.com/{CrunchyrollManager.Instance.CrunOptions.HistoryLang}/watch/{crBrowseEpisode.Id}/";
                    calEpisode.ThumbnailUrl = crBrowseEpisode.Images.Thumbnail?.FirstOrDefault()?.FirstOrDefault()?.Source ?? ""; //https://www.crunchyroll.com/i/coming_soon_beta_thumb.jpg
                    calEpisode.IsPremiumOnly = crBrowseEpisode.EpisodeMetadata.IsPremiumOnly;
                    calEpisode.IsPremiere = crBrowseEpisode.EpisodeMetadata.Episode == "1";
                    calEpisode.SeasonName = crBrowseEpisode.EpisodeMetadata.SeasonTitle;
                    calEpisode.EpisodeNumber = crBrowseEpisode.EpisodeMetadata.Episode;
                    calEpisode.CrSeriesID = crBrowseEpisode.EpisodeMetadata.SeriesId;

                    var existingEpisode = calendarDay.CalendarEpisodes
                        .FirstOrDefault(e => e.SeasonName == calEpisode.SeasonName);

                    if (existingEpisode != null){
                        if (!int.TryParse(existingEpisode.EpisodeNumber, out _)){
                            existingEpisode.EpisodeNumber = "...";
                        } else{
                            var existingNumbers = existingEpisode.EpisodeNumber
                                .Split('-')
                                .Select(n => int.TryParse(n, out var num) ? num : 0)
                                .Where(n => n > 0)
                                .ToList();

                            if (int.TryParse(calEpisode.EpisodeNumber, out var newEpisodeNumber)){
                                existingNumbers.Add(newEpisodeNumber);
                            }

                            existingNumbers.Sort();
                            var lowest = existingNumbers.First();
                            var highest = existingNumbers.Last();

                            // Update the existing episode's number to the new range
                            existingEpisode.EpisodeNumber = lowest == highest
                                ? lowest.ToString()
                                : $"{lowest}-{highest}";

                            if (lowest == 1){
                                existingEpisode.IsPremiere = true;
                            }
                        }

                        existingEpisode.CalendarEpisodes.Add(calEpisode);
                    } else{
                        calendarDay.CalendarEpisodes.Add(calEpisode);
                    }
                }
            }

            if (CrunchyrollManager.Instance.CrunOptions.CalendarShowUpcomingEpisodes){
                foreach (var calendarDay in week.CalendarDays){
                    if (calendarDay.DateTime.Date >= DateTime.Now.Date){
                        if (ProgramManager.Instance.AnilistUpcoming.ContainsKey(calendarDay.DateTime.ToString("yyyy-MM-dd"))){
                            var list = ProgramManager.Instance.AnilistUpcoming[calendarDay.DateTime.ToString("yyyy-MM-dd")];

                            foreach (var calendarEpisode in list.Where(calendarEpisode => calendarDay.DateTime.Date.Day == calendarEpisode.DateTime.Date.Day)
                                         .Where(calendarEpisode => calendarDay.CalendarEpisodes.All(ele => ele.CrSeriesID != calendarEpisode.CrSeriesID && ele.SeasonName != calendarEpisode.SeasonName))){
                                calendarDay.CalendarEpisodes.Add(calendarEpisode);
                            }
                        }
                    }
                }
            }

            foreach (var weekCalendarDay in week.CalendarDays){
                if (weekCalendarDay.CalendarEpisodes.Count > 0)
                    weekCalendarDay.CalendarEpisodes = weekCalendarDay.CalendarEpisodes
                        .OrderBy(e => e.AnilistEpisode) // False first, then true
                        .ThenBy(e => e.DateTime)
                        .ThenBy(e => e.SeasonName)
                        .ThenBy(e => {
                            double parsedNumber;
                            return double.TryParse(e.EpisodeNumber, out parsedNumber) ? parsedNumber : double.MinValue;
                        })
                        .ToList();
            }
        }


        // foreach (var day in week.CalendarDays){
        //     if (day.CalendarEpisodes != null) day.CalendarEpisodes = day.CalendarEpisodes.OrderBy(e => e.DateTime).ToList();
        // }

        calendar["C" + calTargetDate.ToString("yyyy-MM-dd")] = week;


        return week;
    }


    private async Task LoadAnilistUpcoming(){
        DateTime today = DateTime.Today;

        string formattedDate = today.ToString("yyyy-MM-dd");

        if (ProgramManager.Instance.AnilistUpcoming.ContainsKey(formattedDate)){
            return;
        }

        DateTimeOffset todayMidnight = DateTimeOffset.Now.Date;

        long todayMidnightUnix = todayMidnight.ToUnixTimeSeconds();
        long sevenDaysLaterUnix = todayMidnight.AddDays(8).ToUnixTimeSeconds();

        AniListResponseCalendar? aniListResponse = null;

        int currentPage = 1; // Start from page 1
        bool hasNextPage;

        do{
            var variables = new{
                weekStart = todayMidnightUnix,
                weekEnd = sevenDaysLaterUnix,
                page = currentPage
            };

            var payload = new{
                query,
                variables
            };

            string jsonPayload = JsonConvert.SerializeObject(payload, Formatting.Indented);

            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrls.Anilist){
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            var response = await HttpClientReq.Instance.SendHttpRequest(request);

            if (!response.IsOk){
                Console.Error.WriteLine("Anilist Request Failed for upcoming calendar episodes");
                return;
            }

            AniListResponseCalendar currentResponse = Helpers.Deserialize<AniListResponseCalendar>(
                response.ResponseContent, CrunchyrollManager.Instance.SettingsJsonSerializerSettings
            ) ?? new AniListResponseCalendar();


            aniListResponse ??= currentResponse;

            if (aniListResponse != currentResponse){
                aniListResponse.Data?.Page?.AiringSchedules?.AddRange(currentResponse.Data?.Page?.AiringSchedules ??[]);
            }

            hasNextPage = currentResponse.Data?.Page?.PageInfo?.HasNextPage ?? false;

            currentPage++;
        } while (hasNextPage && currentPage < 20);


        var list = aniListResponse.Data?.Page?.AiringSchedules ??[];

        list = list.Where(ele => ele.Media?.ExternalLinks != null && ele.Media.ExternalLinks.Any(external =>
            string.Equals(external.Site, "Crunchyroll", StringComparison.OrdinalIgnoreCase))).ToList();

        List<CalendarEpisode> calendarEpisodes =[];

        foreach (var anilistEle in list){
            var calEp = new CalendarEpisode();

            calEp.DateTime = DateTimeOffset.FromUnixTimeSeconds(anilistEle.AiringAt).UtcDateTime.ToLocalTime();
            calEp.HasPassed = false;
            calEp.EpisodeName = anilistEle.Media?.Title.English;
            calEp.SeriesUrl = $"https://www.crunchyroll.com/{CrunchyrollManager.Instance.CrunOptions.HistoryLang}/series/";
            calEp.EpisodeUrl = $"https://www.crunchyroll.com/{CrunchyrollManager.Instance.CrunOptions.HistoryLang}/watch/";
            calEp.ThumbnailUrl = anilistEle.Media?.CoverImage.ExtraLarge ?? ""; //https://www.crunchyroll.com/i/coming_soon_beta_thumb.jpg
            calEp.IsPremiumOnly = true;
            calEp.IsPremiere = anilistEle.Episode == 1;
            calEp.SeasonName = anilistEle.Media?.Title.English;
            calEp.EpisodeNumber = anilistEle.Episode.ToString();
            calEp.AnilistEpisode = true;

            if (anilistEle.Media?.ExternalLinks != null){
                var url = anilistEle.Media.ExternalLinks.First(external =>
                    string.Equals(external.Site, "Crunchyroll", StringComparison.OrdinalIgnoreCase)).Url;

                string pattern = @"series\/([^\/]+)";

                Match match = Regex.Match(url, pattern);
                string crunchyrollId;
                if (match.Success){
                    crunchyrollId = match.Groups[1].Value;

                    AdjustReleaseTimeToHistory(calEp, crunchyrollId);
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
                            crunchyrollId = match2.Groups[1].Value;

                            AdjustReleaseTimeToHistory(calEp, crunchyrollId);
                        }
                    }
                }
            }

            calendarEpisodes.Add(calEp);
        }

        foreach (var calendarEpisode in calendarEpisodes){
            var airDate = calendarEpisode.DateTime.ToString("yyyy-MM-dd");

            if (!ProgramManager.Instance.AnilistUpcoming.TryGetValue(airDate, out var value)){
                value = new List<CalendarEpisode>();
                ProgramManager.Instance.AnilistUpcoming[airDate] = value;
            }

            value.Add(calendarEpisode);
        }
    }

    private static void AdjustReleaseTimeToHistory(CalendarEpisode calEp, string crunchyrollId){
        calEp.CrSeriesID = crunchyrollId;

        if (CrunchyrollManager.Instance.CrunOptions.History){
            var historySeries = CrunchyrollManager.Instance.HistoryList.FirstOrDefault(item => item.SeriesId == crunchyrollId);

            if (historySeries != null){
                var oldestRelease = DateTime.MinValue;
                foreach (var historySeriesSeason in historySeries.Seasons){
                    if (historySeriesSeason.EpisodesList.Any()){
                        var releaseDate = historySeriesSeason.EpisodesList.Last().EpisodeCrPremiumAirDate;

                        if (releaseDate.HasValue && oldestRelease < releaseDate.Value){
                            oldestRelease = releaseDate.Value;
                        }
                    }
                }

                if (oldestRelease != DateTime.MinValue){
                    var adjustedDate = new DateTime(
                        calEp.DateTime.Year,
                        calEp.DateTime.Month,
                        calEp.DateTime.Day,
                        oldestRelease.Hour,
                        oldestRelease.Minute,
                        oldestRelease.Second,
                        calEp.DateTime.Kind
                    );
                    
                    if ((adjustedDate - oldestRelease).TotalDays is < 6 and > 1){
                        adjustedDate = oldestRelease.AddDays(7);
                    }

                    calEp.DateTime = adjustedDate;
                }
            }
        }
    }

    #region Query

    private string query = @"query ($weekStart: Int, $weekEnd: Int, $page: Int) {
  Page(page: $page) {
    pageInfo {
      hasNextPage
      total
    }
    airingSchedules(
      airingAt_greater: $weekStart
      airingAt_lesser: $weekEnd
    ) {
      id
      episode
      airingAt
      media {
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
        synonyms
        episodes
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
      }
    }
  }
}";

    #endregion
}