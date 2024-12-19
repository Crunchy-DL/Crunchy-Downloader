using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Utils;
using CRD.Utils.Ffmpeg_Encoding;
using CRD.Utils.Structs;
using CRD.Views;
using DynamicData;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogEncodingPresetViewModel : ViewModelBase{
    private readonly ContentDialog dialog;

    [ObservableProperty]
    private bool _editMode;

    [ObservableProperty]
    private string _presetName;

    [ObservableProperty]
    private string _codec;

    [ObservableProperty]
    private ComboBoxItem _selectedResolution = new();

    [ObservableProperty]
    private double? _crf = 23;

    [ObservableProperty]
    private double? _frameRate = 30;

    [ObservableProperty]
    private string _additionalParametersString = "";

    [ObservableProperty]
    private ObservableCollection<StringItem> _additionalParameters = new();

    [ObservableProperty]
    private VideoPreset? _selectedCustomPreset;

    [ObservableProperty]
    private bool _fileExists;

    public ObservableCollection<VideoPreset> CustomPresetsList{ get; } = new(){ };

    public ObservableCollection<ComboBoxItem> ResolutionList{ get; } = new(){
        new ComboBoxItem(){ Content = "3840:2160" }, // 4K UHD
        new ComboBoxItem(){ Content = "3440:1440" }, // Ultra-Wide Quad HD
        new ComboBoxItem(){ Content = "2560:1440" }, // 1440p
        new ComboBoxItem(){ Content = "2560:1080" }, // Ultra-Wide Full HD
        new ComboBoxItem(){ Content = "2160:1080" }, // 2:1 Aspect Ratio
        new ComboBoxItem(){ Content = "1920:1080" }, // 1080p Full HD
        new ComboBoxItem(){ Content = "1920:800" }, // Cinematic 2.40:1
        new ComboBoxItem(){ Content = "1600:900" }, // 900p
        new ComboBoxItem(){ Content = "1366:768" }, // 768p
        new ComboBoxItem(){ Content = "1280:960" }, // SXGA 4:3
        new ComboBoxItem(){ Content = "1280:720" }, // 720p HD
        new ComboBoxItem(){ Content = "1024:576" }, // 576p
        new ComboBoxItem(){ Content = "960:540" }, // 540p qHD
        new ComboBoxItem(){ Content = "854:480" }, // 480p
        new ComboBoxItem(){ Content = "800:600" }, // SVGA
        new ComboBoxItem(){ Content = "768:432" }, // 432p
        new ComboBoxItem(){ Content = "720:480" }, // NTSC SD
        new ComboBoxItem(){ Content = "704:576" }, // PAL SD
        new ComboBoxItem(){ Content = "640:360" }, // 360p
        new ComboBoxItem(){ Content = "426:240" }, // 240p
        new ComboBoxItem(){ Content = "320:240" }, // QVGA
        new ComboBoxItem(){ Content = "320:180" }, // 180p
        new ComboBoxItem(){ Content = "256:144" }, // 144p
    };

    public ContentDialogEncodingPresetViewModel(ContentDialog dialog, bool editMode){
        this.dialog = dialog;

        if (dialog is null){
            throw new ArgumentNullException(nameof(dialog));
        }

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
        FrameRate = double.Parse(value.FrameRate ?? "0");

        SelectedResolution = ResolutionList.FirstOrDefault(e => e.Content?.ToString() == value.Resolution) ?? ResolutionList.First();
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
        RaisePropertyChanged(nameof(AdditionalParametersString));
    }

    [RelayCommand]
    public void RemoveAdditionalParam(StringItem param){
        AdditionalParameters.Remove(param);
        RaisePropertyChanged(nameof(AdditionalParameters));
    }

    private void SaveButton(ContentDialog sender, ContentDialogButtonClickEventArgs args){
        dialog.PrimaryButtonClick -= SaveButton;

        if (EditMode){
            if (SelectedCustomPreset != null){
                var oldName = SelectedCustomPreset.PresetName;

                SelectedCustomPreset.PresetName = PresetName;
                SelectedCustomPreset.Codec = Codec;
                SelectedCustomPreset.FrameRate = Math.Clamp((int)(FrameRate ?? 1), 1, 999).ToString();
                SelectedCustomPreset.Crf = Math.Clamp((int)(Crf ?? 0), 0, 51);
                SelectedCustomPreset.Resolution = SelectedResolution.Content?.ToString() ?? "1920:1080";
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
                FrameRate = Math.Clamp((int)(FrameRate ?? 1), 1, 999).ToString(),
                Crf = Math.Clamp((int)(Crf ?? 0), 0, 51),
                Resolution = SelectedResolution.Content?.ToString() ?? "1920:1080",
                AdditionalParameters = AdditionalParameters.Select(additionalParameter => additionalParameter.stringValue).ToList()
            };

            CfgManager.WriteJsonToFile(Path.Combine(CfgManager.PathENCODING_PRESETS_DIR, newPreset.PresetName + ".json"), newPreset);

            FfmpegEncoding.AddPreset(newPreset);
        }
    }

    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}