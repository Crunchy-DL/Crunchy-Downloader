using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Structs.Crunchyroll.Music;
using CRD.Utils.Structs.History;
using CRD.Utils.UI;
using DynamicData;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogFeaturedMusicViewModel : ViewModelBase{
    private readonly CustomContentDialog dialog;

    [ObservableProperty]
    private ObservableCollection<HistoryEpisode> _featuredMusicList = new();

    [ObservableProperty]
    private bool _musicInHistory;

    private CrunchyMusicVideoList featuredMusic;

    public ContentDialogFeaturedMusicViewModel(CustomContentDialog contentDialog, CrunchyMusicVideoList featuredMusic, bool crunOptionsHistoryIncludeCrArtists){
        ArgumentNullException.ThrowIfNull(contentDialog);

        this.featuredMusic = featuredMusic;

        dialog = contentDialog;
        dialog.Closed += DialogOnClosed;
        dialog.PrimaryButtonClick += SaveButton;

        if (crunOptionsHistoryIncludeCrArtists){
            var episodeList = featuredMusic.Data
                .Select(video =>
                    CrunchyrollManager.Instance.HistoryList
                        .FirstOrDefault(h => h.SeriesId == video.GetSeriesId())?
                        .Seasons.FirstOrDefault(s => s.SeasonId == video.GetSeasonId())?
                        .EpisodesList.FirstOrDefault(e => e.EpisodeId == video.GetEpisodeId()))
                .Where(episode => episode != null)
                .ToList();

            if (episodeList.Count > 0){
                FeaturedMusicList.Clear();
                FeaturedMusicList!.AddRange(episodeList);
            }
        } else{
            List<HistoryEpisode> episodeList =[];

            foreach (var crunchyMusicVideo in featuredMusic.Data){
                var newHistoryEpisode = new HistoryEpisode{
                    EpisodeTitle = crunchyMusicVideo.GetEpisodeTitle(),
                    EpisodeDescription = crunchyMusicVideo.GetEpisodeDescription(),
                    EpisodeId = crunchyMusicVideo.GetEpisodeId(),
                    Episode = crunchyMusicVideo.GetEpisodeNumber(),
                    EpisodeSeasonNum = crunchyMusicVideo.GetSeasonNum(),
                    SpecialEpisode = crunchyMusicVideo.IsSpecialEpisode(),
                    HistoryEpisodeAvailableDubLang = crunchyMusicVideo.GetEpisodeAvailableDubLang(),
                    HistoryEpisodeAvailableSoftSubs = crunchyMusicVideo.GetEpisodeAvailableSoftSubs(),
                    EpisodeCrPremiumAirDate = crunchyMusicVideo.GetAvailableDate(),
                    EpisodeType = crunchyMusicVideo.GetEpisodeType(),
                    IsEpisodeAvailableOnStreamingService = true,
                    ThumbnailImageUrl = crunchyMusicVideo.GetImageUrl(),
                };
                episodeList.Add(newHistoryEpisode);
                newHistoryEpisode.LoadImage();
            }

            if (episodeList.Count > 0){
                FeaturedMusicList.Clear();
                FeaturedMusicList.AddRange(episodeList);
            }
        }
    }

    [RelayCommand]
    public void DownloadEpisode(HistoryEpisode episode){
        episode.DownloadEpisode();
    }

    private void SaveButton(ContentDialog sender, ContentDialogButtonClickEventArgs args){
        dialog.PrimaryButtonClick -= SaveButton;
    }

    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}