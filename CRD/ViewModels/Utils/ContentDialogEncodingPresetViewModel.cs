using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Utils.Ffmpeg_Encoding;
using CRD.Utils.Files;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Views;
using DynamicData;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogEncodingPresetViewModel : ViewModelBase{
    private readonly ContentDialog dialog;

    [ObservableProperty]
    private bool editMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPreview))]
    private string presetName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCodec))]
    [NotifyPropertyChangedFor(nameof(CommandPreview))]
    private string codec;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPreview))]
    private StringItemWithDisplayName selectedResolution = new(){ value = "1920:1080", DisplayName = "1080p exact (1920:1080)" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPreview))]
    private double? crf = 23;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPreview))]
    private string frameRate = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPreview))]
    private string additionalParametersString = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPreview))]
    private ObservableCollection<StringItem> additionalParameters = new();

    [ObservableProperty]
    private VideoPreset? selectedCustomPreset;

    [ObservableProperty]
    private bool fileExists;

    public bool HasCodec => !string.IsNullOrWhiteSpace(Codec);
    public string CommandPreview => BuildCommandPreview();

    public ObservableCollection<VideoPreset> CustomPresetsList{ get; } = new(){ };

    public ObservableCollection<StringItemWithDisplayName> ResolutionList{ get; } = new(){
        new(){ value = "3840:2160", DisplayName = "4K exact (3840:2160)" },
        new(){ value = "-2:2160", DisplayName = "4K keep AR (-2:2160)" },
        new(){ value = "3440:1440", DisplayName = "UWQHD exact (3440:1440)" },
        new(){ value = "2560:1440", DisplayName = "1440p exact (2560:1440)" },
        new(){ value = "-2:1440", DisplayName = "1440p keep AR (-2:1440)" },
        new(){ value = "2560:1080", DisplayName = "UW FHD exact (2560:1080)" },
        new(){ value = "2160:1080", DisplayName = "2:1 exact (2160:1080)" },
        new(){ value = "1920:1080", DisplayName = "1080p exact (1920:1080)" },
        new(){ value = "-2:1080", DisplayName = "1080p keep AR (-2:1080)" },
        new(){ value = "1920:800", DisplayName = "Cinema exact (1920:800)" },
        new(){ value = "1600:900", DisplayName = "900p exact (1600:900)" },
        new(){ value = "1366:768", DisplayName = "768p exact (1366:768)" },
        new(){ value = "1280:960", DisplayName = "SXGA exact (1280:960)" },
        new(){ value = "1280:720", DisplayName = "720p exact (1280:720)" },
        new(){ value = "-2:720", DisplayName = "720p keep AR (-2:720)" },
        new(){ value = "1024:576", DisplayName = "576p exact (1024:576)" },
        new(){ value = "-2:576", DisplayName = "576p keep AR (-2:576)" },
        new(){ value = "960:540", DisplayName = "540p exact (960:540)" },
        new(){ value = "-2:540", DisplayName = "540p keep AR (-2:540)" },
        new(){ value = "854:480", DisplayName = "480p exact (854:480)" },
        new(){ value = "-2:480", DisplayName = "480p keep AR (-2:480)" },
        new(){ value = "800:600", DisplayName = "SVGA exact (800:600)" },
        new(){ value = "768:432", DisplayName = "432p exact (768:432)" },
        new(){ value = "-2:432", DisplayName = "432p keep AR (-2:432)" },
        new(){ value = "720:480", DisplayName = "NTSC exact (720:480)" },
        new(){ value = "704:576", DisplayName = "PAL exact (704:576)" },
        new(){ value = "640:360", DisplayName = "360p exact (640:360)" },
        new(){ value = "-2:360", DisplayName = "360p keep AR (-2:360)" },
        new(){ value = "426:240", DisplayName = "240p exact (426:240)" },
        new(){ value = "-2:240", DisplayName = "240p keep AR (-2:240)" },
        new(){ value = "320:240", DisplayName = "QVGA exact (320:240)" },
        new(){ value = "320:180", DisplayName = "180p exact (320:180)" },
        new(){ value = "-2:180", DisplayName = "180p keep AR (-2:180)" },
        new(){ value = "256:144", DisplayName = "144p exact (256:144)" },
        new(){ value = "-2:144", DisplayName = "144p keep AR (-2:144)" },
    };

    public ContentDialogEncodingPresetViewModel(ContentDialog dialog, bool editMode){
        this.dialog = dialog;

        if (dialog is null){
            throw new ArgumentNullException(nameof(dialog));
        }
        
        AdditionalParameters.Add(new StringItem(){ stringValue = "-map 0" });
        AdditionalParameters.CollectionChanged += AdditionalParametersOnCollectionChanged;

        if (editMode){
            EditMode = true;
            CustomPresetsList.AddRange(FfmpegEncoding.presets.Skip(15));

            this.dialog.Title = "Edit Encoding Preset";
            
            if (CustomPresetsList.Count == 0){
                MessageBus.Current.SendMessage(new ToastMessage($"There are no presets to be edited", ToastType.Warning, 5));
                EditMode = false;
            } else{
                SelectedCustomPreset = CustomPresetsList.First();
            }
        }


        dialog.Closed += DialogOnClosed;
        dialog.PrimaryButtonClick += SaveButton;
    }

    partial void OnSelectedCustomPresetChanged(VideoPreset value){
        PresetName = value.PresetName ?? "";
        Codec = value.Codec ?? "";
        Crf = value.Crf;
        FrameRate = value.FrameRate ?? "24000/1001";

        SelectedResolution = ResolutionList.FirstOrDefault(e => e.value == value.Resolution) ?? ResolutionList.First();
        AdditionalParameters.Clear();

        foreach (var valueAdditionalParameter in value.AdditionalParameters){
            AdditionalParameters.Add(new StringItem(){ stringValue = valueAdditionalParameter });
        }

        AdditionalParametersString = "";
    }

    partial void OnPresetNameChanged(string value){
        var path = Path.Combine(CfgManager.PathENCODING_PRESETS_DIR, value + ".json");
        var fileExists = File.Exists(path);

        dialog.IsPrimaryButtonEnabled = !fileExists || EditMode && value == SelectedCustomPreset?.PresetName;

        FileExists = !dialog.IsPrimaryButtonEnabled;
    }

    [RelayCommand]
    public void AddAdditionalParam(){
        AdditionalParameters.Add(new StringItem(){ stringValue = AdditionalParametersString });
        AdditionalParametersString = "";
    }

    [RelayCommand]
    public void RemoveAdditionalParam(StringItem param){
        AdditionalParameters.Remove(param);
    }

    private void AdditionalParametersOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e){
        OnPropertyChanged(nameof(CommandPreview));
    }

    private string BuildCommandPreview(){
        var previewPreset = new VideoPreset{
            PresetName = PresetName,
            Codec = Codec,
            FrameRate = string.IsNullOrWhiteSpace(FrameRate) ? "24000/1001" : FrameRate,
            Crf = Math.Clamp((int)(Crf ?? 0), 0, 51),
            Resolution = SelectedResolution.value,
            AdditionalParameters = AdditionalParameters
                .Select(additionalParameter => additionalParameter.stringValue)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList()
        };

        var args = Helpers.BuildFFmpegArgsForPreset(
            "S01E01.mkv",
            previewPreset,
            "S01E01_output.mkv");

        return Helpers.BuildCommandString("ffmpeg", args);
    }

    private void SaveButton(ContentDialog sender, ContentDialogButtonClickEventArgs args){
        dialog.PrimaryButtonClick -= SaveButton;

        if (EditMode){
            if (SelectedCustomPreset != null){
                var oldName = SelectedCustomPreset.PresetName;

                SelectedCustomPreset.PresetName = PresetName;
                SelectedCustomPreset.Codec = Codec;
                SelectedCustomPreset.FrameRate = FrameRate;
                SelectedCustomPreset.Crf = Math.Clamp((int)(Crf ?? 0), 0, 51);
                SelectedCustomPreset.Resolution = SelectedResolution.value;
                SelectedCustomPreset.AdditionalParameters = AdditionalParameters.Select(additionalParameter => additionalParameter.stringValue).ToList();

                try{
                    var oldPath = Path.Combine(CfgManager.PathENCODING_PRESETS_DIR, oldName + ".json");
                    var path = Path.Combine(CfgManager.PathENCODING_PRESETS_DIR, SelectedCustomPreset.PresetName + ".json");

                    if (File.Exists(oldPath)){
                        File.Delete(oldPath);
                    }

                    CfgManager.WriteJsonToFile(path, SelectedCustomPreset);
                } catch (Exception e){
                    Console.Error.WriteLine("Error saving preset: " + e);
                }
            }
        } else{
            VideoPreset newPreset = new VideoPreset(){
                PresetName = PresetName,
                Codec = Codec,
                FrameRate = FrameRate,
                Crf = Math.Clamp((int)(Crf ?? 0), 0, 51),
                Resolution = SelectedResolution.value,
                AdditionalParameters = AdditionalParameters.Select(additionalParameter => additionalParameter.stringValue).ToList()
            };

            CfgManager.WriteJsonToFile(Path.Combine(CfgManager.PathENCODING_PRESETS_DIR, newPreset.PresetName + ".json"), newPreset);

            FfmpegEncoding.AddPreset(newPreset);
        }
    }

    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        AdditionalParameters.CollectionChanged -= AdditionalParametersOnCollectionChanged;
        dialog.Closed -= DialogOnClosed;
    }
}
