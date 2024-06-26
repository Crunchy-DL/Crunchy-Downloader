﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Utils.Structs;

namespace CRD.ViewModels;

public partial class DownloadsPageViewModel : ViewModelBase{
    public ObservableCollection<DownloadItemModel> Items{ get; }

    [ObservableProperty]
    public bool _autoDownload;

    [ObservableProperty]
    public bool _removeFinished;
    
    public DownloadsPageViewModel(){
        Crunchyroll.Instance.UpdateDownloadListItems();
        Items = Crunchyroll.Instance.DownloadItemModels;
        AutoDownload = Crunchyroll.Instance.CrunOptions.AutoDownload;
        RemoveFinished = Crunchyroll.Instance.CrunOptions.RemoveFinishedDownload;
    }

    partial void OnAutoDownloadChanged(bool value){
        Crunchyroll.Instance.CrunOptions.AutoDownload = value;
        if (value){
            Crunchyroll.Instance.UpdateDownloadListItems();
        }
    }

    partial void OnRemoveFinishedChanged(bool value){
        Crunchyroll.Instance.CrunOptions.RemoveFinishedDownload = value;
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
        Title = epMeta.SeriesTitle + " - S" + epMeta.Season + "E" + (epMeta.EpisodeNumber != string.Empty ? epMeta.EpisodeNumber : epMeta.AbsolutEpisodeNumberE) + " - " + epMeta.EpisodeTitle;
        isDownloading = epMeta.DownloadProgress.IsDownloading || Done;

        Done = epMeta.DownloadProgress.Done;
        Percent = epMeta.DownloadProgress.Percent;
        Time = "Estimated Time: " + TimeSpan.FromSeconds(epMeta.DownloadProgress.Time / 1000).ToString(@"hh\:mm\:ss");
        DownloadSpeed = $"{epMeta.DownloadProgress.DownloadSpeed / 1000000.0:F2}Mb/s";
        Paused = epMeta.Paused || !isDownloading && !epMeta.Paused;
        DoingWhat = epMeta.Paused ? "Paused" : Done ? "Done" : epMeta.DownloadProgress.Doing != string.Empty ? epMeta.DownloadProgress.Doing : "Waiting";

        if (epMeta.Data != null) InfoText = GetDubString() + " - " + GetSubtitleString();

        Error = epMeta.DownloadProgress.Error;
    }

    private string GetDubString(){
        var hardSubs = Crunchyroll.Instance.CrunOptions.Hslang != "none" ? "Hardsub: " + Crunchyroll.Instance.CrunOptions.Hslang : "";
        if (hardSubs != string.Empty){
            return hardSubs;
        }

        var dubs = "Dub: ";

        if (epMeta.SelectedDubs != null)
            foreach (var crunOptionsDlDub in epMeta.SelectedDubs){
                dubs += crunOptionsDlDub + " ";
            }

        return dubs;
    }

    private string GetSubtitleString(){
        var hardSubs = Crunchyroll.Instance.CrunOptions.Hslang != "none" ? "Hardsub: " + Crunchyroll.Instance.CrunOptions.Hslang : "";
        if (hardSubs != string.Empty){
            return hardSubs;
        }

        var softSubs = "Softsub: ";

        if (Crunchyroll.Instance.CrunOptions.DlSubs.Contains("all")){
            if (epMeta.AvailableSubs != null){
                foreach (var epMetaAvailableSub in epMeta.AvailableSubs){
                    softSubs += epMetaAvailableSub + " ";
                }

                return softSubs;
            }
        }

        foreach (var crunOptionsDlSub in Crunchyroll.Instance.CrunOptions.DlSubs){
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
        Time = "Estimated Time: " + TimeSpan.FromSeconds(epMeta.DownloadProgress.Time / 1000).ToString(@"hh\:mm\:ss");
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
            await Crunchyroll.Instance.DownloadEpisode(epMeta, Crunchyroll.Instance.CrunOptions);
        }
    }

    [RelayCommand]
    public void RemoveFromQueue(){
        CrunchyEpMeta? downloadItem = Crunchyroll.Instance.Queue.FirstOrDefault(e => e.Equals(epMeta)) ?? null;
        if (downloadItem != null){
            Crunchyroll.Instance.Queue.Remove(downloadItem);
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