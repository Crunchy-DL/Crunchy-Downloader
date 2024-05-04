using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Views;
using ReactiveUI;

namespace CRD.ViewModels;

public partial class SeriesPageViewModel : ViewModelBase{


    [ObservableProperty]
    public HistorySeries _selectedSeries;
    
    public SeriesPageViewModel(){
        _selectedSeries = Crunchyroll.Instance.SelectedSeries;
        
        if (_selectedSeries.ThumbnailImage == null){
            _selectedSeries.LoadImage();
        }
    }
    
    [RelayCommand]
    public async Task UpdateData(string? season){
        await SelectedSeries.FetchData(season);

        MessageBus.Current.SendMessage(new NavigationMessage(typeof(SeriesPageViewModel),false,true));
    }
    
    [RelayCommand]
    public void NavBack(){
        SelectedSeries.UpdateNewEpisodes();
        MessageBus.Current.SendMessage(new NavigationMessage(null,true,false));
    }
    
}