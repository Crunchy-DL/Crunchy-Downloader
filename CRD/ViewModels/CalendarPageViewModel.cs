using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Utils;
using CRD.Utils.Structs;
using DynamicData;

namespace CRD.ViewModels;

public partial class CalendarPageViewModel : ViewModelBase{
    public ObservableCollection<CalendarDay> CalendarDays{ get; set; }


    [ObservableProperty]
    private bool _showLoading;

    [ObservableProperty]
    private bool _customCalendar;

    [ObservableProperty]
    private bool _filterByAirDate;

    [ObservableProperty]
    private bool _hideDubs;

    public ObservableCollection<ComboBoxItem> CalendarDubFilter{ get; } = new(){
        new ComboBoxItem(){ Content = "none" },
    };

    [ObservableProperty]
    private ComboBoxItem? _currentCalendarDubFilter;

    public ObservableCollection<ComboBoxItem> CalendarLanguage{ get; } = new(){
        new ComboBoxItem(){ Content = "en-us" },
        new ComboBoxItem(){ Content = "es" },
        new ComboBoxItem(){ Content = "es-es" },
        new ComboBoxItem(){ Content = "pt-br" },
        new ComboBoxItem(){ Content = "pt-pt" },
        new ComboBoxItem(){ Content = "fr" },
        new ComboBoxItem(){ Content = "de" },
        new ComboBoxItem(){ Content = "ar" },
        new ComboBoxItem(){ Content = "it" },
        new ComboBoxItem(){ Content = "ru" },
        new ComboBoxItem(){ Content = "hi" },
    };

    [ObservableProperty]
    private ComboBoxItem? _currentCalendarLanguage;

    private CalendarWeek? currentWeek;

    private bool loading = true;

    public CalendarPageViewModel(){
        CalendarDays = new ObservableCollection<CalendarDay>();

        foreach (var languageItem in Languages.languages){
            CalendarDubFilter.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
        }

        CustomCalendar = Crunchyroll.Instance.CrunOptions.CustomCalendar;
        HideDubs = Crunchyroll.Instance.CrunOptions.CalendarHideDubs;
        FilterByAirDate = Crunchyroll.Instance.CrunOptions.CalendarFilterByAirDate;

        ComboBoxItem? dubfilter = CalendarDubFilter.FirstOrDefault(a => a.Content != null && (string)a.Content == Crunchyroll.Instance.CrunOptions.CalendarDubFilter) ?? null;
        CurrentCalendarDubFilter = dubfilter ?? CalendarDubFilter[0];

        CurrentCalendarLanguage = CalendarLanguage.FirstOrDefault(a => a.Content != null && (string)a.Content == Crunchyroll.Instance.CrunOptions.SelectedCalendarLanguage) ?? CalendarLanguage[0];
        loading = false;
        LoadCalendar(GetThisWeeksMondayDate(), false);
    }

    private string GetThisWeeksMondayDate(){
        // Get today's date
        DateTime today = DateTime.Today;

        // Calculate the number of days to subtract to get to Monday
        // DayOfWeek.Monday is 1, so if today is Monday, subtract 0 days, if it's Tuesday subtract 1 day, etc.
        int daysToSubtract = (int)today.DayOfWeek - (int)DayOfWeek.Monday;

        // If today is Sunday (0), it will subtract -1, which we need to adjust to 6 to go back to the previous Monday
        if (daysToSubtract < 0){
            daysToSubtract += 7;
        }

        // Get the date of the most recent Monday
        DateTime monday = today.AddDays(-daysToSubtract);

        // Format and print the date
        string formattedDate = monday.ToString("yyyy-MM-dd");

        return formattedDate;
    }

    public async void LoadCalendar(string mondayDate, bool forceUpdate){
        if (CustomCalendar){
            BuildCustomCalendar();
            return;
        }

        ShowLoading = true;
        CalendarWeek week = await Crunchyroll.Instance.GetCalendarForDate(mondayDate, forceUpdate);
        if (currentWeek != null && currentWeek == week){
            ShowLoading = false;
            return;
        }

        currentWeek = week;
        CalendarDays.Clear();
        CalendarDays.AddRange(week.CalendarDays);
        RaisePropertyChanged(nameof(CalendarDays));
        ShowLoading = false;

        foreach (var calendarDay in CalendarDays){
            var episodesCopy = new List<CalendarEpisode>(calendarDay.CalendarEpisodes);
            foreach (var calendarDayCalendarEpisode in episodesCopy){
                if (calendarDayCalendarEpisode.SeasonName != null && HideDubs && calendarDayCalendarEpisode.SeasonName.EndsWith("Dub)")){
                    calendarDay.CalendarEpisodes.Remove(calendarDayCalendarEpisode);
                    continue;
                }

                if (calendarDayCalendarEpisode.ImageBitmap == null){
                    calendarDayCalendarEpisode.LoadImage();
                }
            }
        }
    }

    private string NextMonday(DateTime currentMonday){
        DateTime nextMonday = currentMonday.AddDays(7);
        return nextMonday.ToString("yyyy-MM-dd");
    }

    private string PreviousMonday(DateTime currentMonday){
        DateTime nextMonday = currentMonday.AddDays(-7);
        return nextMonday.ToString("yyyy-MM-dd");
    }


    [RelayCommand]
    public void Refresh(){
        if (loading){
            return;
        }

        if (CustomCalendar){
            BuildCustomCalendar();
            return;
        }

        string mondayDate;

        if (currentWeek is{ FirstDayOfWeekString: not null }){
            mondayDate = currentWeek.FirstDayOfWeekString;
        } else{
            mondayDate = GetThisWeeksMondayDate();
        }

        LoadCalendar(mondayDate, true);
    }

    [RelayCommand]
    public void PrevWeek(){
        if (loading){
            return;
        }

        string mondayDate;

        if (currentWeek is{ FirstDayOfWeek: not null }){
            mondayDate = PreviousMonday((DateTime)currentWeek.FirstDayOfWeek);
        } else{
            mondayDate = GetThisWeeksMondayDate();
        }

        LoadCalendar(mondayDate, false);
    }

    [RelayCommand]
    public void NextWeek(){
        if (loading){
            return;
        }

        string mondayDate;

        if (currentWeek is{ FirstDayOfWeek: not null }){
            mondayDate = NextMonday((DateTime)currentWeek.FirstDayOfWeek);
        } else{
            mondayDate = GetThisWeeksMondayDate();
        }

        LoadCalendar(mondayDate, false);
    }


    partial void OnCurrentCalendarLanguageChanged(ComboBoxItem? value){
        if (loading){
            return;
        }

        if (value?.Content != null){
            Crunchyroll.Instance.CrunOptions.SelectedCalendarLanguage = value.Content.ToString();
            Refresh();
            CfgManager.WriteSettingsToFile();
        }
    }

    partial void OnCustomCalendarChanged(bool value){
        if (loading){
            return;
        }

        if (CustomCalendar){
            BuildCustomCalendar();
        } else{
            LoadCalendar(GetThisWeeksMondayDate(), true);
        }

        Crunchyroll.Instance.CrunOptions.CustomCalendar = value;
        CfgManager.WriteSettingsToFile();
    }

    partial void OnHideDubsChanged(bool value){
        if (loading){
            return;
        }

        Crunchyroll.Instance.CrunOptions.CalendarHideDubs = value;
        CfgManager.WriteSettingsToFile();
    }
    
    partial void OnFilterByAirDateChanged(bool value){
        if (loading){
            return;
        }

        Crunchyroll.Instance.CrunOptions.CalendarFilterByAirDate = value;
        CfgManager.WriteSettingsToFile();
    }

    partial void OnCurrentCalendarDubFilterChanged(ComboBoxItem? value){
        if (loading){
            return;
        }

        if (!string.IsNullOrEmpty(value?.Content + "")){
            Crunchyroll.Instance.CrunOptions.CalendarDubFilter = value?.Content + "";
            CfgManager.WriteSettingsToFile();
        }
    }

    private async void BuildCustomCalendar(){
        ShowLoading = true;

        var newEpisodesBase = await Crunchyroll.Instance.CrEpisode.GetNewEpisodes(Crunchyroll.Instance.CrunOptions.HistoryLang, 200);

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

        if (newEpisodesBase is{ Data.Count: > 0 }){
            var newEpisodes = newEpisodesBase.Data;

            foreach (var crBrowseEpisode in newEpisodes){
                var targetDate = FilterByAirDate ? crBrowseEpisode.EpisodeMetadata.EpisodeAirDate : crBrowseEpisode.LastPublic;
                
                if (HideDubs && crBrowseEpisode.EpisodeMetadata.SeasonTitle != null && crBrowseEpisode.EpisodeMetadata.SeasonTitle.EndsWith("Dub)")){
                    continue;
                }

                var dubFilter = CurrentCalendarDubFilter?.Content + "";
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
                    calEpisode.SeriesUrl = "https://www.crunchyroll.com/series/" + crBrowseEpisode.EpisodeMetadata.SeriesId;
                    calEpisode.EpisodeUrl = $"https://www.crunchyroll.com/de/watch/{crBrowseEpisode.Id}/";
                    calEpisode.ThumbnailUrl = crBrowseEpisode.Images.Thumbnail.First().First().Source;
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

        currentWeek = week;
        CalendarDays.Clear();
        CalendarDays.AddRange(week.CalendarDays);
        RaisePropertyChanged(nameof(CalendarDays));
        ShowLoading = false;
        foreach (var calendarDay in CalendarDays){
            foreach (var calendarDayCalendarEpisode in calendarDay.CalendarEpisodes){
                if (calendarDayCalendarEpisode.ImageBitmap == null){
                    calendarDayCalendarEpisode.LoadImage();
                }
            }
        }
    }
}