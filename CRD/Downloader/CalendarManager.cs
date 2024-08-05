using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Structs;
using HtmlAgilityPack;

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

        var request = calendarLanguage.ContainsKey(CrunchyrollManager.Instance.CrunOptions.SelectedCalendarLanguage ?? "de")
            ? HttpClientReq.CreateRequestMessage($"{calendarLanguage[CrunchyrollManager.Instance.CrunOptions.SelectedCalendarLanguage ?? "de"]}?filter=premium&date={weeksMondayDate}", HttpMethod.Get, false, false, null)
            : HttpClientReq.CreateRequestMessage($"{calendarLanguage["en-us"]}?filter=premium&date={weeksMondayDate}", HttpMethod.Get, false, false, null);


        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

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
                DateTime dayDateTime = DateTime.Parse(date, null, DateTimeStyles.RoundtripKind);

                if (week.FirstDayOfWeek == null){
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

                week.CalendarDays.Add(calDay);
            }
        } else{
            Console.Error.WriteLine("No days found in the HTML document.");
        }

        calendar[weeksMondayDate] = week;


        return week;
    }


    public async Task<CalendarWeek> BuildCustomCalendar(bool forceUpdate){
        if (!forceUpdate && calendar.TryGetValue("C" + DateTime.Now.ToString("yyyy-MM-dd"), out var forDate)){
            return forDate;
        }

    

        CalendarWeek week = new CalendarWeek();
        week.CalendarDays = new List<CalendarDay>();

        DateTime today = DateTime.Now;

        for (int i = 0; i < 7; i++){
            CalendarDay calDay = new CalendarDay();

            calDay.CalendarEpisodes = new List<CalendarEpisode>();
            calDay.DateTime = today.AddDays(-i);
            calDay.DayName = calDay.DateTime.Value.DayOfWeek.ToString();

            week.CalendarDays.Add(calDay);
        }

        week.CalendarDays.Reverse();
        
        var firstDayOfWeek = week.CalendarDays.First().DateTime;

        var newEpisodesBase = await CrunchyrollManager.Instance.CrEpisode.GetNewEpisodes(CrunchyrollManager.Instance.CrunOptions.HistoryLang, 200,firstDayOfWeek, true);
        
        if (newEpisodesBase is{ Data.Count: > 0 }){
            var newEpisodes = newEpisodesBase.Data;

            foreach (var crBrowseEpisode in newEpisodes){
                var targetDate = CrunchyrollManager.Instance.CrunOptions.CalendarFilterByAirDate ? crBrowseEpisode.EpisodeMetadata.EpisodeAirDate : crBrowseEpisode.LastPublic;

                if (targetDate.Kind == DateTimeKind.Utc){
                    targetDate = targetDate.ToLocalTime();
                }
                
                if (CrunchyrollManager.Instance.CrunOptions.CalendarHideDubs && crBrowseEpisode.EpisodeMetadata.SeasonTitle != null &&
                    (crBrowseEpisode.EpisodeMetadata.SeasonTitle.EndsWith("Dub)") || crBrowseEpisode.EpisodeMetadata.AudioLocale != Locale.JaJp)){
                    continue;
                }

                var dubFilter = CrunchyrollManager.Instance.CrunOptions.CalendarDubFilter;
                if (!string.IsNullOrEmpty(dubFilter) && dubFilter != "none"){
                    if (crBrowseEpisode.EpisodeMetadata.AudioLocale != null && crBrowseEpisode.EpisodeMetadata.AudioLocale.GetEnumMemberValue() != dubFilter){
                        continue;
                    }
                }

                var calendarDay = (from day in week.CalendarDays
                    where day.DateTime.HasValue && day.DateTime.Value.Date == targetDate.Date
                    select day).FirstOrDefault();

                if (calendarDay != null){
                    CalendarEpisode calEpisode = new CalendarEpisode();

                    calEpisode.DateTime = targetDate;
                    calEpisode.HasPassed = DateTime.Now > targetDate;
                    calEpisode.EpisodeName = crBrowseEpisode.Title;
                    calEpisode.SeriesUrl = $"https://www.crunchyroll.com/{CrunchyrollManager.Instance.CrunOptions.HistoryLang}/series/" + crBrowseEpisode.EpisodeMetadata.SeriesId;
                    calEpisode.EpisodeUrl = $"https://www.crunchyroll.com/{CrunchyrollManager.Instance.CrunOptions.HistoryLang}/watch/{crBrowseEpisode.Id}/";
                    calEpisode.ThumbnailUrl = crBrowseEpisode.Images.Thumbnail?.FirstOrDefault()?.FirstOrDefault().Source ?? ""; //https://www.crunchyroll.com/i/coming_soon_beta_thumb.jpg
                    calEpisode.IsPremiumOnly = crBrowseEpisode.EpisodeMetadata.IsPremiumOnly;
                    calEpisode.IsPremiere = crBrowseEpisode.EpisodeMetadata.Episode == "1";
                    calEpisode.SeasonName = crBrowseEpisode.EpisodeMetadata.SeasonTitle;
                    calEpisode.EpisodeNumber = crBrowseEpisode.EpisodeMetadata.Episode;

                    calendarDay.CalendarEpisodes?.Add(calEpisode);
                }
            }
        }


        foreach (var day in week.CalendarDays){
            if (day.CalendarEpisodes != null) day.CalendarEpisodes = day.CalendarEpisodes.OrderBy(e => e.DateTime).ToList();
        }

        calendar["C" + DateTime.Now.ToString("yyyy-MM-dd")] = week;


        return week;
    }
}