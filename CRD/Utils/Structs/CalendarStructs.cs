using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;

namespace CRD.Utils.Structs;

public class CalendarWeek{
    public DateTime FirstDayOfWeek{ get; set; }
    public string? FirstDayOfWeekString{ get; set; }
    public List<CalendarDay>? CalendarDays{ get; set; }
}

public class CalendarDay{
    public DateTime DateTime{ get; set; }
    public string? DayName{ get; set; }
    public List<CalendarEpisode> CalendarEpisodes{ get; set; } =[];
}

public partial class CalendarEpisode : INotifyPropertyChanged{
    public DateTime DateTime{ get; set; }
    public bool? HasPassed{ get; set; }
    public string? EpisodeName{ get; set; }
    public string? SeriesUrl{ get; set; }
    public string? EpisodeUrl{ get; set; }
    public string? ThumbnailUrl{ get; set; }
    public Bitmap? ImageBitmap{ get; set; }

    public string? EpisodeNumber{ get; set; }

    public bool IsPremiumOnly{ get; set; }
    public bool IsPremiere{ get; set; }

    public string? SeasonName{ get; set; }

    public string? CrSeriesID{ get; set; }
    
    public bool AnilistEpisode{ get; set; }

    public List<CalendarEpisode> CalendarEpisodes{ get; set; } =[];

    public event PropertyChangedEventHandler? PropertyChanged;

    [RelayCommand]
    public void AddEpisodeToQue(){
        if (CalendarEpisodes.Count > 0){
            foreach (var calendarEpisode in CalendarEpisodes){
                calendarEpisode.AddEpisodeToQue();
            }
        }

        if (EpisodeUrl != null){
            var match = Regex.Match(EpisodeUrl, "/([^/]+)/watch/([^/]+)");

            if (match.Success){
                var locale = match.Groups[1].Value; // Capture the locale part
                var id = match.Groups[2].Value; // Capture the ID part
                QueueManager.Instance.CrAddEpisodeToQueue(id, Languages.Locale2language(locale).CrLocale, CrunchyrollManager.Instance.CrunOptions.DubLang, true);
            }
        }
    }

    public async Task LoadImage(int width = 0, int height = 0){
        try{
            if (!string.IsNullOrEmpty(ThumbnailUrl)){
                ImageBitmap = await Helpers.LoadImage(ThumbnailUrl, width, height);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageBitmap)));
            }
        } catch (Exception ex){
            // Handle exceptions
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
        }
    }
}