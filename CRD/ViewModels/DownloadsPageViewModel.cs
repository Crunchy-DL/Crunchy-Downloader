using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Structs;

namespace CRD.ViewModels;

public partial class DownloadsPageViewModel : ViewModelBase{
    public ObservableCollection<DownloadItemModel> Items{ get; }

    [ObservableProperty]
    private bool _autoDownload;

    [ObservableProperty]
    private bool _removeFinished;

    public DownloadsPageViewModel(){
        QueueManager.Instance.UpdateDownloadListItems();
        Items = QueueManager.Instance.DownloadItemModels;
        AutoDownload = CrunchyrollManager.Instance.CrunOptions.AutoDownload;
        RemoveFinished = CrunchyrollManager.Instance.CrunOptions.RemoveFinishedDownload;
    }

    partial void OnAutoDownloadChanged(bool value){
        CrunchyrollManager.Instance.CrunOptions.AutoDownload = value;
        if (value){
            QueueManager.Instance.UpdateDownloadListItems();
        }

        CfgManager.WriteSettingsToFile();
    }

    partial void OnRemoveFinishedChanged(bool value){
        CrunchyrollManager.Instance.CrunOptions.RemoveFinishedDownload = value;
        CfgManager.WriteSettingsToFile();
    }
}

public partial class DownloadItemModel : INotifyPropertyChanged{
    public string ImageUrl{ get; set; }
    public Bitmap? ImageBitmap{ get; set; }
    public string Title{ get; set; }

    public bool isDownloading{ get; set; }
    public bool Done{ get; set; }
    public bool Paused{ get; set; }

    public double Percent{ get; set; }
    public string Time{ get; set; }
    public string DoingWhat{ get; set; }
    public string DownloadSpeed{ get; set; }
    public string InfoText{ get; set; }

    public CrunchyEpMeta epMeta{ get; set; }


    public bool Error{ get; set; }

    public DownloadItemModel(CrunchyEpMeta epMetaF){
        epMeta = epMetaF;

        ImageUrl = epMeta.Image;
        Title = epMeta.SeriesTitle + (!string.IsNullOrEmpty(epMeta.Season) ? " - S" + epMeta.Season + "E" + (epMeta.EpisodeNumber != string.Empty ? epMeta.EpisodeNumber : epMeta.AbsolutEpisodeNumberE) : "") + " - " +
                epMeta.EpisodeTitle;
        isDownloading = epMeta.DownloadProgress.IsDownloading || Done;

        Done = epMeta.DownloadProgress.Done;
        Percent = epMeta.DownloadProgress.Percent;
        Time = "Estimated Time: " + TimeSpan.FromSeconds(epMeta.DownloadProgress.Time).ToString(@"hh\:mm\:ss");
        DownloadSpeed = $"{epMeta.DownloadProgress.DownloadSpeed / 1000000.0:F2}Mb/s";
        Paused = epMeta.Paused || !isDownloading && !epMeta.Paused;
        DoingWhat = epMeta.Paused ? "Paused" : Done ? "Done" : epMeta.DownloadProgress.Doing != string.Empty ? epMeta.DownloadProgress.Doing : "Waiting";

        if (epMeta.Data != null) InfoText = GetDubString() + " - " + GetSubtitleString();

        Error = epMeta.DownloadProgress.Error;
    }

    private string GetDubString(){
        if (epMeta.SelectedDubs == null || epMeta.SelectedDubs.Count < 1){
            return "";
        }
        
        return epMeta.SelectedDubs.Aggregate("Dub: ", (current, crunOptionsDlDub) => current + (crunOptionsDlDub + " "));
    }

    private string GetSubtitleString(){
        var hardSubs = CrunchyrollManager.Instance.CrunOptions.Hslang != "none" ? "Hardsub: " : "";
        if (hardSubs != string.Empty){
            var locale = Languages.Locale2language(CrunchyrollManager.Instance.CrunOptions.Hslang).CrLocale;
            if (epMeta.AvailableSubs != null && epMeta.AvailableSubs.Contains(locale)){
                hardSubs += locale + " ";
            }

            return hardSubs;
        }

        if (epMeta.DownloadSubs.Count < 1){
            return "";
        }

        var softSubs = "Softsub: ";

        if (epMeta.DownloadSubs.Contains("all")){
            if (epMeta.AvailableSubs != null){
                return epMeta.AvailableSubs.Aggregate(softSubs, (current, epMetaAvailableSub) => current + (epMetaAvailableSub + " "));
            }
        }

        foreach (var crunOptionsDlSub in epMeta.DownloadSubs){
            if (epMeta.AvailableSubs != null && epMeta.AvailableSubs.Contains(crunOptionsDlSub)){
                softSubs += crunOptionsDlSub + " ";
            }
        }

        return softSubs;
    }

    public void Refresh(){
        isDownloading = epMeta.DownloadProgress.IsDownloading || Done;
        Done = epMeta.DownloadProgress.Done;
        Percent = epMeta.DownloadProgress.Percent;
        Time = "Estimated Time: " + TimeSpan.FromSeconds(epMeta.DownloadProgress.Time).ToString(@"hh\:mm\:ss");
        DownloadSpeed = $"{epMeta.DownloadProgress.DownloadSpeed / 1000000.0:F2}Mb/s";

        Paused = epMeta.Paused || !isDownloading && !epMeta.Paused;
        DoingWhat = epMeta.Paused ? "Paused" : Done ? "Done" : epMeta.DownloadProgress.Doing != string.Empty ? epMeta.DownloadProgress.Doing : "Waiting";

        if (epMeta.Data != null) InfoText = GetDubString() + " - " + GetSubtitleString();

        Error = epMeta.DownloadProgress.Error;

        if (PropertyChanged != null){
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(isDownloading)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Percent)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Time)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownloadSpeed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DoingWhat)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InfoText)));
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    [RelayCommand]
    public void ToggleIsDownloading(){
        if (isDownloading){
            //StopDownload();
            epMeta.Paused = !epMeta.Paused;

            Paused = epMeta.Paused || !isDownloading && !epMeta.Paused;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Paused)));
        } else{
            if (epMeta.Paused){
                epMeta.Paused = false;
                Paused = epMeta.Paused || !isDownloading && !epMeta.Paused;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Paused)));
            } else{
                StartDownload();
            }
        }


        if (PropertyChanged != null){
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs("isDownloading"));
        }
    }

    public async void StartDownload(){
        if (!isDownloading){
            isDownloading = true;
            epMeta.DownloadProgress.IsDownloading = true;
            Paused = !epMeta.Paused && !isDownloading || epMeta.Paused;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Paused)));
            await CrunchyrollManager.Instance.DownloadEpisode(epMeta, CrunchyrollManager.Instance.CrunOptions);
        }
    }

    [RelayCommand]
    public void RemoveFromQueue(){
        CrunchyEpMeta? downloadItem = QueueManager.Instance.Queue.FirstOrDefault(e => e.Equals(epMeta)) ?? null;
        if (downloadItem != null){
            QueueManager.Instance.Queue.Remove(downloadItem);
        }
    }

    public async Task LoadImage(){
        try{
            using (var client = new HttpClient()){
                var response = await client.GetAsync(ImageUrl);
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync()){
                    ImageBitmap = new Bitmap(stream);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageBitmap)));
                }
            }
        } catch (Exception ex){
            // Handle exceptions
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
        }
    }
}