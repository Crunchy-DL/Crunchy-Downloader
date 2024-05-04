using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Views;
using ReactiveUI;

namespace CRD.Utils.Structs;

public class CalendarWeek{
    public DateTime? FirstDayOfWeek{ get; set; }
    public string? FirstDayOfWeekString{ get; set; }
    public List<CalendarDay>? CalendarDays{ get; set; }
}

public class CalendarDay{
    public DateTime? DateTime{ get; set; }
    public string? DayName{ get; set; }
    public List<CalendarEpisode>? CalendarEpisodes{ get; set; }
}

public partial class CalendarEpisode : INotifyPropertyChanged{
    public DateTime? DateTime{ get; set; }
    public bool? HasPassed{ get; set; }
    public string? EpisodeName{ get; set; }
    public string? SeasonUrl{ get; set; }
    public string? EpisodeUrl{ get; set; }
    public string? ThumbnailUrl{ get; set; }
    public Bitmap? ImageBitmap{ get; set; }

    public string? EpisodeNumber{ get; set; }
    
    public bool IsPremiumOnly{ get; set; }

    public string? SeasonName{ get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    [RelayCommand]
    public void AddEpisodeToQue(string episodeUrl){
        var match = Regex.Match(episodeUrl, "/([^/]+)/watch/([^/]+)");

        if (match.Success){
            var locale = match.Groups[1].Value; // Capture the locale part
            var id = match.Groups[2].Value; // Capture the ID part
            Crunchyroll.Instance.AddEpisodeToQue(id, locale, Crunchyroll.Instance.CrunOptions.DubLang);
        }
    }

    public async Task LoadImage(){
        try{
            using (var client = new HttpClient()){
                var response = await client.GetAsync(ThumbnailUrl);
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync()){
                    ImageBitmap = new Bitmap(stream);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageBitmap)));
                }
            }
        } catch (Exception ex){
            // Handle exceptions
            Console.WriteLine("Failed to load image: " + ex.Message);
        }
    }
}