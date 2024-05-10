using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Views;
using ReactiveUI;

namespace CRD.ViewModels;

public partial class HistoryPageViewModel : ViewModelBase{
    
    public ObservableCollection<HistorySeries> Items{ get; }
    [ObservableProperty] private bool? _showLoading = false;
    [ObservableProperty]
    public HistorySeries _selectedSeries;

    public HistoryPageViewModel(){
        Items = Crunchyroll.Instance.HistoryList;
        
        foreach (var historySeries in Items){
            if (historySeries.ThumbnailImage == null){
                historySeries.LoadImage();
            }
            historySeries.UpdateNewEpisodes();
        }

    }


    partial void OnSelectedSeriesChanged(HistorySeries value){
        Crunchyroll.Instance.SelectedSeries = value;
        MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel),false,false));
        _selectedSeries = null;
    }

    [RelayCommand]
    public void NavToSeries(){
        MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel),false,false));
    }
    
    [RelayCommand]
    public async void RefreshAll(){
        for (int i = 0; i < Items.Count; i++) {
            ShowLoading = true;
            await Items[i].FetchData("");
            Items[i].UpdateNewEpisodes();
        }
        ShowLoading = false;
    }
    
    [RelayCommand]
    public async void AddMissingToQueue(){
        for (int i = 0; i < Items.Count; i++) {
            await Items[i].AddNewMissingToDownloads();
        }
        ShowLoading = false;
    }
    
    
}