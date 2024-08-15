using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Sonarr;
using CRD.Utils.Sonarr.Models;
using DynamicData;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogSonarrMatchViewModel : ViewModelBase{
    private readonly ContentDialog dialog;

    [ObservableProperty]
    private SonarrSeries _currentSonarrSeries;

    [ObservableProperty]
    private Bitmap? _currentSeriesImage;

    [ObservableProperty]
    private SonarrSeries _selectedItem;

    [ObservableProperty]
    private ObservableCollection<SonarrSeries> _sonarrSeriesList = new();

    public ContentDialogSonarrMatchViewModel(ContentDialog dialog, string? currentSonarrId, string? seriesTitle){
        if (dialog is null){
            throw new ArgumentNullException(nameof(dialog));
        }

        this.dialog = dialog;
        dialog.Closed += DialogOnClosed;
        dialog.PrimaryButtonClick += SaveButton;

        CurrentSonarrSeries = SonarrClient.Instance.SonarrSeries.Find(e => e.Id.ToString() == currentSonarrId) ?? new SonarrSeries(){ Title = "No series matched" };

        SetImageUrl(CurrentSonarrSeries);

        LoadList(seriesTitle);
    }

    private void SaveButton(ContentDialog sender, ContentDialogButtonClickEventArgs args){
        dialog.PrimaryButtonClick -= SaveButton;
        
    }

    private void LoadList(string? title){
        var list = PopulateSeriesList(title);
        SonarrSeriesList.AddRange(list);
    }

    private List<SonarrSeries> PopulateSeriesList(string? title){
        var seriesList = SonarrClient.Instance.SonarrSeries.ToList();


        if (!string.IsNullOrEmpty(title)){
            seriesList.Sort((series1, series2) => {
                double similarity1 = Helpers.CalculateCosineSimilarity(series1.Title.ToLower(), title.ToLower());
                double similarity2 = Helpers.CalculateCosineSimilarity(series2.Title.ToLower(), title.ToLower());

                return similarity2.CompareTo(similarity1);
            });
        } else{
            seriesList.Sort((series1, series2) => string.Compare(series1.Title, series2.Title, StringComparison.OrdinalIgnoreCase));
        }

        seriesList = seriesList.Take(20).ToList();

        foreach (var sonarrSeries in seriesList){
            SetImageUrl(sonarrSeries);
        }

        return seriesList;
    }

    private void SetImageUrl(SonarrSeries sonarrSeries){
        var properties = CrunchyrollManager.Instance.CrunOptions.SonarrProperties;
        if (properties == null || sonarrSeries.Images == null){
            return;
        }

        var baseUrl = "";
        baseUrl = $"http{(properties.UseSsl ? "s" : "")}://{(!string.IsNullOrEmpty(properties.Host) ? properties.Host : "localhost")}:{properties.Port}{(properties.UrlBase ?? "")}";

        sonarrSeries.ImageUrl = baseUrl + sonarrSeries.Images.Find(e => e.CoverType == SonarrCoverType.Poster)?.Url;
    }


    partial void OnSelectedItemChanged(SonarrSeries value){
        CurrentSonarrSeries = value;
    }

    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}