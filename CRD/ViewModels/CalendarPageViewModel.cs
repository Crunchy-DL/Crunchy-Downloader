using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
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

        CustomCalendar = CrunchyrollManager.Instance.CrunOptions.CustomCalendar;
        HideDubs = CrunchyrollManager.Instance.CrunOptions.CalendarHideDubs;
        FilterByAirDate = CrunchyrollManager.Instance.CrunOptions.CalendarFilterByAirDate;

        ComboBoxItem? dubfilter = CalendarDubFilter.FirstOrDefault(a => a.Content != null && (string)a.Content == CrunchyrollManager.Instance.CrunOptions.CalendarDubFilter) ?? null;
        CurrentCalendarDubFilter = dubfilter ?? CalendarDubFilter[0];

        CurrentCalendarLanguage = CalendarLanguage.FirstOrDefault(a => a.Content != null && (string)a.Content == CrunchyrollManager.Instance.CrunOptions.SelectedCalendarLanguage) ?? CalendarLanguage[0];
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
        ShowLoading = true;

        CalendarWeek week;

        if (CustomCalendar){
            week = await CalendarManager.Instance.BuildCustomCalendar(forceUpdate);
        } else{
            week = await CalendarManager.Instance.GetCalendarForDate(mondayDate, forceUpdate);
            if (currentWeek != null && currentWeek == week){
                ShowLoading = false;
                return;
            }
        }

        currentWeek = week;
        CalendarDays.Clear();
        CalendarDays.AddRange(week.CalendarDays);
        RaisePropertyChanged(nameof(CalendarDays));
        ShowLoading = false;
        if (CustomCalendar){
            foreach (var calendarDay in CalendarDays){
                foreach (var calendarDayCalendarEpisode in calendarDay.CalendarEpisodes){
                    if (calendarDayCalendarEpisode.ImageBitmap == null){
                        calendarDayCalendarEpisode.LoadImage();
                    }
                }
            }
        } else{
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
            CrunchyrollManager.Instance.CrunOptions.SelectedCalendarLanguage = value.Content.ToString();
            Refresh();
            CfgManager.WriteSettingsToFile();
        }
    }

    partial void OnCustomCalendarChanged(bool value){
        if (loading){
            return;
        }

        CrunchyrollManager.Instance.CrunOptions.CustomCalendar = value;

        LoadCalendar(GetThisWeeksMondayDate(), true);

        CfgManager.WriteSettingsToFile();
    }

    partial void OnHideDubsChanged(bool value){
        if (loading){
            return;
        }

        CrunchyrollManager.Instance.CrunOptions.CalendarHideDubs = value;
        CfgManager.WriteSettingsToFile();
    }

    partial void OnFilterByAirDateChanged(bool value){
        if (loading){
            return;
        }

        CrunchyrollManager.Instance.CrunOptions.CalendarFilterByAirDate = value;
        CfgManager.WriteSettingsToFile();
    }

    partial void OnCurrentCalendarDubFilterChanged(ComboBoxItem? value){
        if (loading){
            return;
        }

        if (!string.IsNullOrEmpty(value?.Content + "")){
            CrunchyrollManager.Instance.CrunOptions.CalendarDubFilter = value?.Content + "";
            CfgManager.WriteSettingsToFile();
        }
    }
}