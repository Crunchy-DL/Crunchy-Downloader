﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll;

namespace CRD.ViewModels;

public partial class DownloadsPageViewModel : ViewModelBase{
    public ObservableCollection<DownloadItemModel> Items{ get; }

    [ObservableProperty]
    private bool _shutdownWhenQueueEmpty;
    
    [ObservableProperty]
    private bool _autoDownload;

    [ObservableProperty]
    private bool _removeFinished;
    
    [ObservableProperty]
    private QueueManager _queueManagerIns;
    
    public DownloadsPageViewModel(){
        QueueManagerIns = QueueManager.Instance;
        QueueManagerIns.UpdateDownloadListItems();
        Items = QueueManagerIns.DownloadItemModels;
        AutoDownload = CrunchyrollManager.Instance.CrunOptions.AutoDownload;
        RemoveFinished = CrunchyrollManager.Instance.CrunOptions.RemoveFinishedDownload;
        ShutdownWhenQueueEmpty = CrunchyrollManager.Instance.CrunOptions.ShutdownWhenQueueEmpty;
    }
    

    partial void OnAutoDownloadChanged(bool value){
        CrunchyrollManager.Instance.CrunOptions.AutoDownload = value;
        if (value){
            QueueManagerIns.UpdateDownloadListItems();
        }

        CfgManager.WriteCrSettings();
    }

    partial void OnRemoveFinishedChanged(bool value){
        CrunchyrollManager.Instance.CrunOptions.RemoveFinishedDownload = value;
        CfgManager.WriteCrSettings();
    }

    partial void OnShutdownWhenQueueEmptyChanged(bool value){
        CrunchyrollManager.Instance.CrunOptions.ShutdownWhenQueueEmpty = value;
        CfgManager.WriteCrSettings();
    }

    [RelayCommand]
    public void ClearQueue(){
        var items = QueueManagerIns.Queue;
        QueueManagerIns.Queue.Clear();
        
        foreach (var crunchyEpMeta in items){
            if (!crunchyEpMeta.DownloadProgress.Done){
                foreach (var downloadItemDownloadedFile in crunchyEpMeta.downloadedFiles){
                    try{
                        if (File.Exists(downloadItemDownloadedFile)){
                            File.Delete(downloadItemDownloadedFile);
                        }
                    } catch (Exception){
                        // ignored
                    }
                }
            }
        }
    }
    
    [RelayCommand]
    public void RetryQueue(){
        var items = QueueManagerIns.Queue;

        foreach (var crunchyEpMeta in items){
            if (crunchyEpMeta.DownloadProgress.Error){
                crunchyEpMeta.DownloadProgress = new();
            }
        }

        QueueManagerIns.UpdateDownloadListItems();
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

        ImageUrl = epMeta.Image ?? string.Empty;
        Title = epMeta.SeriesTitle + (!string.IsNullOrEmpty(epMeta.Season) ? " - S" + epMeta.Season + "E" + (epMeta.EpisodeNumber != string.Empty ? epMeta.EpisodeNumber : epMeta.AbsolutEpisodeNumberE) : "") + " - " +
                epMeta.EpisodeTitle;

        isDownloading = epMeta.DownloadProgress.IsDownloading || Done;

        Done = epMeta.DownloadProgress.Done;
        Percent = epMeta.DownloadProgress.Percent;
        Time = "Estimated Time: " + TimeSpan.FromSeconds(epMeta.DownloadProgress.Time).ToString(@"hh\:mm\:ss");
        DownloadSpeed = $"{epMeta.DownloadProgress.DownloadSpeed / 1000000.0:F2}Mb/s";
        Paused = epMeta.Paused || !isDownloading && !epMeta.Paused;
        DoingWhat = epMeta.Paused ? "Paused" :
            Done ? (epMeta.DownloadProgress.Doing != string.Empty ? epMeta.DownloadProgress.Doing : "Done") :
            epMeta.DownloadProgress.Doing != string.Empty ? epMeta.DownloadProgress.Doing : "Waiting";

        InfoText = JoinWithSeparator(
            GetDubString(),
            GetSubtitleString(),
            epMeta.Resolution
        );

        Error = epMeta.DownloadProgress.Error;
    }

    string JoinWithSeparator(params string[] parts){
        return string.Join(" - ", parts.Where(part => !string.IsNullOrEmpty(part)));
    }

    private string GetDubString(){
        if (epMeta.SelectedDubs == null || epMeta.SelectedDubs.Count < 1){
            return "";
        }

        return epMeta.SelectedDubs.Aggregate("Dub: ", (current, crunOptionsDlDub) => current + (crunOptionsDlDub + " "));
    }

    private string GetSubtitleString(){
        var hardSubs = epMeta.Hslang != "none" ? "Hardsub: " : "";
        if (hardSubs != string.Empty){
            var locale = Languages.Locale2language(epMeta.Hslang).CrLocale;
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
        DoingWhat = epMeta.Paused ? "Paused" :
            Done ? (epMeta.DownloadProgress.Doing != string.Empty ? epMeta.DownloadProgress.Doing : "Done") :
            epMeta.DownloadProgress.Doing != string.Empty ? epMeta.DownloadProgress.Doing : "Waiting";

        InfoText = JoinWithSeparator(
            GetDubString(),
            GetSubtitleString(),
            epMeta.Resolution
        );

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

            CrDownloadOptions newOptions = Helpers.DeepCopy(CrunchyrollManager.Instance.CrunOptions);

            if (epMeta.OnlySubs){
                newOptions.Novids = true;
                newOptions.Noaudio = true;
            }

            await CrunchyrollManager.Instance.DownloadEpisode(epMeta, epMeta.DownloadSettings ?? newOptions);
        }
    }

    [RelayCommand]
    public void RemoveFromQueue(){
        CrunchyEpMeta? downloadItem = QueueManager.Instance.Queue.FirstOrDefault(e => e.Equals(epMeta)) ?? null;
        if (downloadItem != null){
            QueueManager.Instance.Queue.Remove(downloadItem);
            if (!Done){
                foreach (var downloadItemDownloadedFile in downloadItem.downloadedFiles){
                    try{
                        if (File.Exists(downloadItemDownloadedFile)){
                            File.Delete(downloadItemDownloadedFile);
                        }
                    } catch (Exception){
                        // ignored
                    }
                }
            }
        }
    }

    public async Task LoadImage(){
        ImageBitmap = await Helpers.LoadImage(ImageUrl, 208, 117);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageBitmap)));
    }
}