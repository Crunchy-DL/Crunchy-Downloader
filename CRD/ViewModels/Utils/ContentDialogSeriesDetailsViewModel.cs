using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll.Music;
using CRD.Utils.Structs.History;
using CRD.Utils.UI;
using CRD.Views;
using DynamicData;
using FluentAvalonia.UI.Controls;
using ReactiveUI;
using Image = CRD.Utils.Structs.Image;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogSeriesDetailsViewModel : ViewModelBase{
    private readonly CustomContentDialog dialog;

    private CrSeriesBase seriesBase;
    private string downloadPath = CfgManager.PathVIDEOS_DIR;

    [ObservableProperty]
    private ObservableCollection<SeriesDetailsImage> _imagesListPosterTall = new();

    [ObservableProperty]
    private ObservableCollection<SeriesDetailsImage> _imagesListPosterWide = new();

    [ObservableProperty]
    private ObservableCollection<SeriesDetailsImage> _imagesListPromoImage = new();

    [ObservableProperty]
    private ObservableCollection<SeriesDetailsImage> _imagesListThumbnail = new();

    [ObservableProperty]
    private bool _isResolutionPopupOpenTall;

    [ObservableProperty]
    private bool _isResolutionPopupOpenWide;

    [ObservableProperty]
    private Image? _selectedImage;

    public ContentDialogSeriesDetailsViewModel(CustomContentDialog contentDialog, CrSeriesBase seriesBase, string downloadPath){
        ArgumentNullException.ThrowIfNull(contentDialog);

        this.seriesBase = seriesBase;
        if (!string.IsNullOrEmpty(downloadPath)){
            this.downloadPath = downloadPath;
        }


        dialog = contentDialog;
        dialog.Closed += DialogOnClosed;
        dialog.PrimaryButtonClick += SaveButton;

        _ = LoadImages();
    }

    public async Task LoadImages(){
        var images = (seriesBase.Data ??[]).FirstOrDefault()?.Images;
        if (images != null){
            foreach (var list in images.PosterTall){
                ImagesListPosterTall.Add(new SeriesDetailsImage(){
                    images = list,
                    imagePreview = await Helpers.LoadImage(list.Last().Source)
                });
            }

            foreach (var list in images.PosterWide){
                ImagesListPosterWide.Add(new SeriesDetailsImage(){
                    images = list,
                    imagePreview = await Helpers.LoadImage(list.Last().Source)
                });
            }

            foreach (var list in images.PromoImage){
                ImagesListPromoImage.Add(new SeriesDetailsImage(){
                    images = list,
                    imagePreview = await Helpers.LoadImage(list.Last().Source)
                });
            }

            foreach (var list in images.Thumbnail){
                ImagesListThumbnail.Add(new SeriesDetailsImage(){
                    images = list,
                    imagePreview = await Helpers.LoadImage(list.Last().Source)
                });
            }
        }
    }

    [RelayCommand]
    public void ToggleButtonTall(){
        IsResolutionPopupOpenTall = !IsResolutionPopupOpenTall;
    }
    
    [RelayCommand]
    public void ToggleButtonWide(){
        IsResolutionPopupOpenWide = !IsResolutionPopupOpenWide;
    }

    [RelayCommand]
    public async Task DownloadImage(Image? image){
        if (image == null){
            return;
        }

        IsResolutionPopupOpenTall = false;
        IsResolutionPopupOpenWide = false;
        SelectedImage = new Image();

        string fileName = image.Type.GetEnumMemberValue() + "_" + image.Width + "_" + image.Height + ".png";

        string coverPath = Path.Combine(downloadPath, fileName);
        if (!string.IsNullOrEmpty(image.Source)){
            if (!File.Exists(coverPath)){
                var bitmap = await Helpers.LoadImage(image.Source);
                if (bitmap != null){
                    Helpers.EnsureDirectoriesExist(coverPath);
                    await using (var fs = File.OpenWrite(coverPath)){
                        bitmap.Save(fs); // always saves PNG
                    }

                    bitmap.Dispose();
                    MessageBus.Current.SendMessage(new ToastMessage($"Image downloaded: " + coverPath, ToastType.Information, 1));
                }
            } else{
                MessageBus.Current.SendMessage(new ToastMessage($"Image already exists with that name: " + coverPath, ToastType.Error, 3));
            }
        }
    }

    private void SaveButton(ContentDialog sender, ContentDialogButtonClickEventArgs args){
        dialog.PrimaryButtonClick -= SaveButton;
    }

    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}

public class SeriesDetailsImage{
    public List<Image> images{ get; set; }
    public Bitmap? imagePreview{ get; set; }
}