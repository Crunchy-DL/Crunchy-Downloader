using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Sonarr;
using CRD.Utils.Sonarr.Models;
using CRD.Utils.Structs.History;
using CRD.Utils.UI;
using DynamicData;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogSonarrMatchEpisodeViewModel : ViewModelBase{
    private readonly CustomContentDialog dialog;

    [ObservableProperty]
    private SonarrEpisode _currentSonarrEpisode;

    [ObservableProperty]
    private HistoryEpisode _currentHistoryEpisode;

    [ObservableProperty]
    private SonarrEpisode _selectedItem;

    [ObservableProperty]
    private ObservableCollection<SeasonsItem> _sonarrSeasonList = new();

    [ObservableProperty]
    private SonarrSeries _sonarrSeries;

    public ContentDialogSonarrMatchEpisodeViewModel(CustomContentDialog contentDialog, HistorySeries selectedSeries, HistoryEpisode episode){
        ArgumentNullException.ThrowIfNull(contentDialog);

        dialog = contentDialog;
        dialog.Closed += DialogOnClosed;
        dialog.PrimaryButtonClick += SaveButton;

        CurrentHistoryEpisode = episode;

        SonarrSeries = SonarrClient.Instance.SonarrSeries.Find(e => e.Id.ToString() == selectedSeries.SonarrSeriesId) ?? new SonarrSeries(){ Title = "No series matched" };
        SetImageUrl(SonarrSeries);

        _ = LoadList(selectedSeries.SonarrSeriesId);
    }

    private void SaveButton(ContentDialog sender, ContentDialogButtonClickEventArgs args){
        dialog.PrimaryButtonClick -= SaveButton;
    }

    private async Task LoadList(string? sonarrSeriesId){
        if (string.IsNullOrEmpty(sonarrSeriesId)){
            return;
        }

        var list = await PopulateSeriesList(sonarrSeriesId);
        SonarrSeasonList.AddRange(list);
    }

    private async Task<List<SeasonsItem>> PopulateSeriesList(string sonarrSeriesId){
        List<SonarrEpisode> episodes = await SonarrClient.Instance.GetEpisodes(int.Parse(sonarrSeriesId));

        var seasonsDict = new Dictionary<int, SeasonsItem>();

        foreach (var episode in episodes){
            if (CurrentHistoryEpisode.SonarrEpisodeId == episode.Id.ToString()){
                CurrentSonarrEpisode = episode;
            }

            int seasonNumber = episode.SeasonNumber;
            if (!seasonsDict.TryGetValue(seasonNumber, out var season)){
                season = new SeasonsItem{
                    SeasonName = $"Season {seasonNumber}",
                    Episodes = new List<SonarrEpisode>()
                };
                seasonsDict[seasonNumber] = season;
            }

            season.Episodes.Add(episode);
        }

        var seasons = seasonsDict.Values
            .OrderBy(s => int.TryParse(System.Text.RegularExpressions.Regex.Match(s.SeasonName, @"\d+").Value, out int n) ? n : int.MaxValue)
            .ToList();

        foreach (var season in seasons){
            season.Episodes.Sort((a, b) => a.EpisodeNumber.CompareTo(b.EpisodeNumber));
        }

        return seasons;
    }

    private async void SetImageUrl(SonarrSeries sonarrSeries){
        var properties = CrunchyrollManager.Instance.CrunOptions.SonarrProperties;
        if (properties == null || sonarrSeries.Images == null){
            return;
        }

        var baseUrl = "";
        baseUrl = $"http{(properties.UseSsl ? "s" : "")}://{(!string.IsNullOrEmpty(properties.Host) ? properties.Host : "localhost")}:{properties.Port}{(properties.UrlBase ?? "")}";

        sonarrSeries.ImageUrl = baseUrl + sonarrSeries.Images.Find(e => e.CoverType == SonarrCoverType.FanArt)?.Url;

        var image = await Helpers.LoadImage(sonarrSeries.ImageUrl);

        if (image == null) return;
        dialog.BackgroundImage = image;
        dialog.BackgroundImageOpacity = CrunchyrollManager.Instance.CrunOptions.BackgroundImageOpacity;
        dialog.BackgroundImageBlurRadius = CrunchyrollManager.Instance.CrunOptions.BackgroundImageBlurRadius;
    }

    [RelayCommand]
    public void SetSonarrEpisodeMatch(SonarrEpisode episode){
        CurrentSonarrEpisode = episode;
    }

    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}

public class SeasonsItem(){
    public string SeasonName{ get; set; }

    public string IsExpanded{ get; set; }
    public List<SonarrEpisode> Episodes{ get; set; }
}