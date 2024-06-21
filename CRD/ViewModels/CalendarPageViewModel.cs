using System;
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

    [ObservableProperty] private ComboBoxItem? _currentCalendarLanguage;
    [ObservableProperty] private bool? _showLoading = false;

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

    private CalendarWeek? currentWeek;

    public CalendarPageViewModel(){
        CalendarDays = new ObservableCollection<CalendarDay>();
        CurrentCalendarLanguage = CalendarLanguage.FirstOrDefault(a => a.Content != null && (string)a.Content == Crunchyroll.Instance.CrunOptions.SelectedCalendarLanguage) ?? CalendarLanguage[0];
        // LoadCalendar(GetThisWeeksMondayDate(), false);
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
            foreach (var calendarDayCalendarEpisode in calendarDay.CalendarEpisodes){
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
        string mondayDate;

        if (currentWeek is{ FirstDayOfWeek: not null }){
            mondayDate = NextMonday((DateTime)currentWeek.FirstDayOfWeek);
        } else{
            mondayDate = GetThisWeeksMondayDate();
        }

        LoadCalendar(mondayDate, false);
    }


    partial void OnCurrentCalendarLanguageChanged(ComboBoxItem? value){
        if (value?.Content != null){
            Crunchyroll.Instance.CrunOptions.SelectedCalendarLanguage = value.Content.ToString();
            Refresh();
            CfgManager.WriteSettingsToFile();
        }
    }
}