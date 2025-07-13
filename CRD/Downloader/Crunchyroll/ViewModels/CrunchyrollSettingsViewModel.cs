using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Utils;
using CRD.Utils.Ffmpeg_Encoding;
using CRD.Utils.Files;
using CRD.Utils.Sonarr;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll;
using CRD.Utils.Structs.History;
using CRD.ViewModels;
using CRD.ViewModels.Utils;
using CRD.Views.Utils;
using DynamicData;
using FluentAvalonia.UI.Controls;

// ReSharper disable InconsistentNaming

namespace CRD.Downloader.Crunchyroll.ViewModels;

public partial class CrunchyrollSettingsViewModel : ViewModelBase{
    [ObservableProperty]
    private bool _downloadVideo = true;

    [ObservableProperty]
    private bool _downloadAudio = true;

    [ObservableProperty]
    private bool _downloadChapters = true;

    [ObservableProperty]
    private bool _addScaledBorderAndShadow;
    
    [ObservableProperty]
    private bool _subsDownloadDuplicate;
    
    [ObservableProperty]
    private bool _includeSignSubs;

    [ObservableProperty]
    private bool _includeCcSubs;

    [ObservableProperty]
    private ComboBoxItem _selectedScaledBorderAndShadow;

    public ObservableCollection<ComboBoxItem> ScaledBorderAndShadow{ get; } = new(){
        new ComboBoxItem(){ Content = "ScaledBorderAndShadow: yes" },
        new ComboBoxItem(){ Content = "ScaledBorderAndShadow: no" },
    };

    [ObservableProperty]
    private bool _skipMuxing;

    [ObservableProperty]
    private bool _muxToMp4;
    
    [ObservableProperty]
    private bool _muxToMp3;

    [ObservableProperty]
    private bool _muxFonts;

    [ObservableProperty]
    private bool _syncTimings;

    [ObservableProperty]
    private bool _defaultSubSigns;

    [ObservableProperty]
    private bool _defaultSubForcedDisplay;

    [ObservableProperty]
    private bool _includeEpisodeDescription;

    [ObservableProperty]
    private bool _downloadVideoForEveryDub;

    [ObservableProperty]
    private bool _keepDubsSeparate;

    [ObservableProperty]
    private bool _skipSubMux;

    [ObservableProperty]
    private double? _leadingNumbers;

    [ObservableProperty]
    private double? _partSize;

    [ObservableProperty]
    private string _fileName = "";
    
    [ObservableProperty]
    private string _fileNameWhitespaceSubstitute = "";

    [ObservableProperty]
    private string _fileTitle = "";

    [ObservableProperty]
    private ObservableCollection<StringItem> _mkvMergeOptions = new();

    [ObservableProperty]
    private string _mkvMergeOption = "";

    [ObservableProperty]
    private string _ffmpegOption = "";

    [ObservableProperty]
    private ObservableCollection<StringItem> _ffmpegOptions = new();

    [ObservableProperty]
    private string _selectedSubs = "all";

    [ObservableProperty]
    private ComboBoxItem _selectedHSLang;

    [ObservableProperty]
    private ComboBoxItem _selectedDescriptionLang;

    [ObservableProperty]
    private string _selectedDubs = "ja-JP";

    [ObservableProperty]
    private ObservableCollection<ListBoxItem> _selectedDubLang = new();

    [ObservableProperty]
    private ComboBoxItem _selectedStreamEndpoint;
    
    [ObservableProperty]
    private ComboBoxItem _selectedStreamEndpointSecondary;

    [ObservableProperty]
    private ComboBoxItem _selectedDefaultDubLang;

    [ObservableProperty]
    private ComboBoxItem _selectedDefaultSubLang;

    [ObservableProperty]
    private ComboBoxItem? _selectedVideoQuality;

    [ObservableProperty]
    private ComboBoxItem? _selectedAudioQuality;

    [ObservableProperty]
    private ObservableCollection<ListBoxItem> _selectedSubLang =[];

    [ObservableProperty]
    private Color _listBoxColor;

    public ObservableCollection<ComboBoxItem> VideoQualityList{ get; } =[
        new(){ Content = "best" },
        new(){ Content = "1080" },
        new(){ Content = "720" },
        new(){ Content = "480" },
        new(){ Content = "360" },
        new(){ Content = "240" },
        new(){ Content = "worst" }
    ];

    public ObservableCollection<ComboBoxItem> AudioQualityList{ get; } =[
        new(){ Content = "best" },
        new(){ Content = "128kB/s" },
        new(){ Content = "96kB/s" },
        new(){ Content = "64kB/s" },
        new(){ Content = "worst" }
    ];

    public ObservableCollection<ComboBoxItem> HardSubLangList{ get; } =[
        new(){ Content = "none" }
    ];

    public ObservableCollection<ComboBoxItem> DescriptionLangList{ get; } =[
        new(){ Content = "default" },
        new(){ Content = "de-DE" },
        new(){ Content = "en-US" },
        new(){ Content = "es-419" },
        new(){ Content = "es-ES" },
        new(){ Content = "fr-FR" },
        new(){ Content = "it-IT" },
        new(){ Content = "pt-BR" },
        new(){ Content = "pt-PT" },
        new(){ Content = "ru-RU" },
        new(){ Content = "hi-IN" },
        new(){ Content = "ar-SA" }
    ];

    public ObservableCollection<ListBoxItem> DubLangList{ get; } =[];


    public ObservableCollection<ComboBoxItem> DefaultDubLangList{ get; } =[];

    public ObservableCollection<ComboBoxItem> DefaultSubLangList{ get; } =[];


    public ObservableCollection<ListBoxItem> SubLangList{ get; } =[
        new(){ Content = "all" },
        new(){ Content = "none" }
    ];

    public ObservableCollection<ComboBoxItem> StreamEndpoints{ get; } =[
        new(){ Content = "web/firefox" },
        new(){ Content = "console/switch" },
        new(){ Content = "console/ps4" },
        new(){ Content = "console/ps5" },
        new(){ Content = "console/xbox_one" },
        new(){ Content = "web/edge" },
        new(){ Content = "web/chrome" },
        new(){ Content = "web/fallback" },
        new(){ Content = "android/phone" },
        new(){ Content = "android/tablet" },
        new(){ Content = "tv/samsung" },
        new(){ Content = "tv/vidaa" }
    ];
    
    public ObservableCollection<ComboBoxItem> StreamEndpointsSecondary{ get; } =[
        new(){ Content = "" },
        new(){ Content = "web/firefox" },
        new(){ Content = "console/switch" },
        new(){ Content = "console/ps4" },
        new(){ Content = "console/ps5" },
        new(){ Content = "console/xbox_one" },
        new(){ Content = "web/edge" },
        new(){ Content = "web/chrome" },
        new(){ Content = "web/fallback" },
        new(){ Content = "android/phone" },
        new(){ Content = "android/tablet" },
        new(){ Content = "tv/samsung" },
        new(){ Content = "tv/vidaa" }
    ];

    public ObservableCollection<StringItemWithDisplayName> FFmpegHWAccel{ get; } =[];

    [ObservableProperty]
    private StringItemWithDisplayName _selectedFFmpegHWAccel;

    [ObservableProperty]
    private bool _isEncodeEnabled;

    [ObservableProperty]
    private StringItem _selectedEncodingPreset;

    public ObservableCollection<StringItem> EncodingPresetsList{ get; } = new();


    [ObservableProperty]
    private bool _cCSubsMuxingFlag;

    [ObservableProperty]
    private string _cCSubsFont;

    [ObservableProperty]
    private bool _signsSubsAsForced;

    [ObservableProperty]
    private bool _searchFetchFeaturedMusic;

    [ObservableProperty]
    private bool _useCrBetaApi;

    [ObservableProperty]
    private bool _downloadFirstAvailableDub;

    [ObservableProperty]
    private bool _markAsWatched;

    private bool settingsLoaded;

    public CrunchyrollSettingsViewModel(){
        foreach (var languageItem in Languages.languages){
            HardSubLangList.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
            SubLangList.Add(new ListBoxItem{ Content = languageItem.CrLocale });
            DubLangList.Add(new ListBoxItem{ Content = languageItem.CrLocale });
            DefaultDubLangList.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
            DefaultSubLangList.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
        }

        foreach (var encodingPreset in FfmpegEncoding.presets){
            EncodingPresetsList.Add(new StringItem{ stringValue = encodingPreset.PresetName ?? "Unknown Preset Name" });
        }


        CrDownloadOptions options = CrunchyrollManager.Instance.CrunOptions;

        StringItem? encodingPresetSelected = EncodingPresetsList.FirstOrDefault(a => !string.IsNullOrEmpty(a.stringValue) && a.stringValue == options.EncodingPresetName) ?? null;
        SelectedEncodingPreset = encodingPresetSelected ?? EncodingPresetsList[0];

        ComboBoxItem? descriptionLang = DescriptionLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.DescriptionLang) ?? null;
        SelectedDescriptionLang = descriptionLang ?? DescriptionLangList[0];

        ComboBoxItem? hsLang = HardSubLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.Hslang) ?? null;
        SelectedHSLang = hsLang ?? HardSubLangList[0];

        ComboBoxItem? defaultDubLang = DefaultDubLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == (options.DefaultAudio ?? "")) ?? null;
        SelectedDefaultDubLang = defaultDubLang ?? DefaultDubLangList[0];

        ComboBoxItem? defaultSubLang = DefaultSubLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == (options.DefaultSub ?? "")) ?? null;
        SelectedDefaultSubLang = defaultSubLang ?? DefaultSubLangList[0];

        ComboBoxItem? streamEndpoint = StreamEndpoints.FirstOrDefault(a => a.Content != null && (string)a.Content == (options.StreamEndpoint ?? "")) ?? null;
        SelectedStreamEndpoint = streamEndpoint ?? StreamEndpoints[0];

        ComboBoxItem? streamEndpointSecondary = StreamEndpointsSecondary.FirstOrDefault(a => a.Content != null && (string)a.Content == (options.StreamEndpointSecondary ?? "")) ?? null;
        SelectedStreamEndpointSecondary = streamEndpointSecondary ?? StreamEndpointsSecondary[0];
        
        FFmpegHWAccel.AddRange(GetAvailableHWAccelOptions());

        StringItemWithDisplayName? hwAccellFlag = FFmpegHWAccel.FirstOrDefault(a => a.value == options.FfmpegHwAccelFlag) ?? null;
        SelectedFFmpegHWAccel = hwAccellFlag ?? FFmpegHWAccel[0];


        var softSubLang = SubLangList.Where(a => options.DlSubs.Contains(a.Content)).ToList();

        SelectedSubLang.Clear();
        foreach (var listBoxItem in softSubLang){
            SelectedSubLang.Add(listBoxItem);
        }

        var dubLang = DubLangList.Where(a => options.DubLang.Contains(a.Content)).ToList();

        SelectedDubLang.Clear();
        foreach (var listBoxItem in dubLang){
            SelectedDubLang.Add(listBoxItem);
        }

        AddScaledBorderAndShadow = options.SubsAddScaledBorder is ScaledBorderAndShadowSelection.ScaledBorderAndShadowNo or ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes;
        SelectedScaledBorderAndShadow = GetScaledBorderAndShadowFromOptions(options);

        SubsDownloadDuplicate = options.SubsDownloadDuplicate;
        MarkAsWatched = options.MarkAsWatched;
        DownloadFirstAvailableDub = options.DownloadFirstAvailableDub;
        UseCrBetaApi = options.UseCrBetaApi;
        CCSubsFont = options.CcSubsFont ?? "";
        CCSubsMuxingFlag = options.CcSubsMuxingFlag;
        SignsSubsAsForced = options.SignsSubsAsForced;
        SkipMuxing = options.SkipMuxing;
        IsEncodeEnabled = options.IsEncodeEnabled;
        DefaultSubForcedDisplay = options.DefaultSubForcedDisplay;
        DefaultSubSigns = options.DefaultSubSigns;
        PartSize = options.Partsize;
        IncludeEpisodeDescription = options.IncludeVideoDescription;
        FileTitle = options.VideoTitle ?? "";
        IncludeSignSubs = options.IncludeSignsSubs;
        IncludeCcSubs = options.IncludeCcSubs;
        DownloadVideo = !options.Novids;
        DownloadAudio = !options.Noaudio;
        DownloadVideoForEveryDub = !options.DlVideoOnce;
        KeepDubsSeparate = options.KeepDubsSeperate;
        DownloadChapters = options.Chapters;
        MuxToMp4 = options.Mp4;
        MuxToMp3 = options.AudioOnlyToMp3;
        MuxFonts = options.MuxFonts;
        SyncTimings = options.SyncTiming;
        SkipSubMux = options.SkipSubsMux;
        LeadingNumbers = options.Numbers;
        FileName = options.FileName;
        FileNameWhitespaceSubstitute = options.FileNameWhitespaceSubstitute;
        SearchFetchFeaturedMusic = options.SearchFetchFeaturedMusic;

        ComboBoxItem? qualityAudio = AudioQualityList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.QualityAudio) ?? null;
        SelectedAudioQuality = qualityAudio ?? AudioQualityList[0];

        ComboBoxItem? qualityVideo = VideoQualityList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.QualityVideo) ?? null;
        SelectedVideoQuality = qualityVideo ?? VideoQualityList[0];

        MkvMergeOptions.Clear();
        if (options.MkvmergeOptions is{ Count: > 0 }){
            foreach (var mkvmergeParam in options.MkvmergeOptions){
                MkvMergeOptions.Add(new StringItem(){ stringValue = mkvmergeParam });
            }
        }

        FfmpegOptions.Clear();
        if (options.FfmpegOptions is{ Count: > 0 }){
            foreach (var ffmpegParam in options.FfmpegOptions){
                FfmpegOptions.Add(new StringItem(){ stringValue = ffmpegParam });
            }
        }

        var dubs = SelectedDubLang.Select(item => item.Content?.ToString());
        SelectedDubs = string.Join(", ", dubs) ?? "";

        var subs = SelectedSubLang.Select(item => item.Content?.ToString());
        SelectedSubs = string.Join(", ", subs) ?? "";

        SelectedSubLang.CollectionChanged += Changes;
        SelectedDubLang.CollectionChanged += Changes;

        MkvMergeOptions.CollectionChanged += Changes;
        FfmpegOptions.CollectionChanged += Changes;

        settingsLoaded = true;
    }

    private void UpdateSettings(){
        if (!settingsLoaded){
            return;
        }

        CrunchyrollManager.Instance.CrunOptions.SubsDownloadDuplicate = SubsDownloadDuplicate;
        CrunchyrollManager.Instance.CrunOptions.MarkAsWatched = MarkAsWatched;
        CrunchyrollManager.Instance.CrunOptions.DownloadFirstAvailableDub = DownloadFirstAvailableDub;
        CrunchyrollManager.Instance.CrunOptions.UseCrBetaApi = UseCrBetaApi;
        CrunchyrollManager.Instance.CrunOptions.SignsSubsAsForced = SignsSubsAsForced;
        CrunchyrollManager.Instance.CrunOptions.CcSubsMuxingFlag = CCSubsMuxingFlag;
        CrunchyrollManager.Instance.CrunOptions.CcSubsFont = CCSubsFont;
        CrunchyrollManager.Instance.CrunOptions.EncodingPresetName = SelectedEncodingPreset.stringValue;
        CrunchyrollManager.Instance.CrunOptions.IsEncodeEnabled = IsEncodeEnabled;
        CrunchyrollManager.Instance.CrunOptions.DefaultSubSigns = DefaultSubSigns;
        CrunchyrollManager.Instance.CrunOptions.DefaultSubForcedDisplay = DefaultSubForcedDisplay;
        CrunchyrollManager.Instance.CrunOptions.IncludeVideoDescription = IncludeEpisodeDescription;
        CrunchyrollManager.Instance.CrunOptions.VideoTitle = FileTitle;
        CrunchyrollManager.Instance.CrunOptions.Novids = !DownloadVideo;
        CrunchyrollManager.Instance.CrunOptions.Noaudio = !DownloadAudio;
        CrunchyrollManager.Instance.CrunOptions.DlVideoOnce = !DownloadVideoForEveryDub;
        CrunchyrollManager.Instance.CrunOptions.KeepDubsSeperate = KeepDubsSeparate;
        CrunchyrollManager.Instance.CrunOptions.Chapters = DownloadChapters;
        CrunchyrollManager.Instance.CrunOptions.SkipMuxing = SkipMuxing;
        CrunchyrollManager.Instance.CrunOptions.Mp4 = MuxToMp4;
        CrunchyrollManager.Instance.CrunOptions.AudioOnlyToMp3 = MuxToMp3;
        CrunchyrollManager.Instance.CrunOptions.MuxFonts = MuxFonts;
        CrunchyrollManager.Instance.CrunOptions.SyncTiming = SyncTimings;
        CrunchyrollManager.Instance.CrunOptions.SkipSubsMux = SkipSubMux;
        CrunchyrollManager.Instance.CrunOptions.Numbers = Math.Clamp((int)(LeadingNumbers ?? 0), 0, 10);
        CrunchyrollManager.Instance.CrunOptions.FileName = FileName;
        CrunchyrollManager.Instance.CrunOptions.FileNameWhitespaceSubstitute = FileNameWhitespaceSubstitute;
        CrunchyrollManager.Instance.CrunOptions.IncludeSignsSubs = IncludeSignSubs;
        CrunchyrollManager.Instance.CrunOptions.IncludeCcSubs = IncludeCcSubs;
        CrunchyrollManager.Instance.CrunOptions.Partsize = Math.Clamp((int)(PartSize ?? 1), 1, 10000);
        CrunchyrollManager.Instance.CrunOptions.SearchFetchFeaturedMusic = SearchFetchFeaturedMusic;

        CrunchyrollManager.Instance.CrunOptions.SubsAddScaledBorder = GetScaledBorderAndShadowSelection();

        CrunchyrollManager.Instance.CrunOptions.FfmpegHwAccelFlag = SelectedFFmpegHWAccel.value;

        List<string> softSubs = new List<string>();
        foreach (var listBoxItem in SelectedSubLang){
            softSubs.Add(listBoxItem.Content + "");
        }

        CrunchyrollManager.Instance.CrunOptions.DlSubs = softSubs;

        string descLang = SelectedDescriptionLang.Content + "";

        CrunchyrollManager.Instance.CrunOptions.DescriptionLang = descLang != "default" ? descLang : CrunchyrollManager.Instance.DefaultLocale;
        
        CrunchyrollManager.Instance.CrunOptions.Hslang = SelectedHSLang.Content + "";

        CrunchyrollManager.Instance.CrunOptions.DefaultAudio = SelectedDefaultDubLang.Content + "";
        CrunchyrollManager.Instance.CrunOptions.DefaultSub = SelectedDefaultSubLang.Content + "";


        CrunchyrollManager.Instance.CrunOptions.StreamEndpoint = SelectedStreamEndpoint.Content + "";
        CrunchyrollManager.Instance.CrunOptions.StreamEndpointSecondary = SelectedStreamEndpointSecondary.Content + "";

        List<string> dubLangs = new List<string>();
        foreach (var listBoxItem in SelectedDubLang){
            dubLangs.Add(listBoxItem.Content + "");
        }

        CrunchyrollManager.Instance.CrunOptions.DubLang = dubLangs;

        CrunchyrollManager.Instance.CrunOptions.QualityAudio = SelectedAudioQuality?.Content + "";
        CrunchyrollManager.Instance.CrunOptions.QualityVideo = SelectedVideoQuality?.Content + "";

        List<string> mkvmergeParams = new List<string>();
        foreach (var mkvmergeParam in MkvMergeOptions){
            mkvmergeParams.Add(mkvmergeParam.stringValue);
        }

        CrunchyrollManager.Instance.CrunOptions.MkvmergeOptions = mkvmergeParams;

        List<string> ffmpegParams = new List<string>();
        foreach (var ffmpegParam in FfmpegOptions){
            ffmpegParams.Add(ffmpegParam.stringValue);
        }

        CrunchyrollManager.Instance.CrunOptions.FfmpegOptions = ffmpegParams;

        CfgManager.WriteCrSettings();
    }


    private ScaledBorderAndShadowSelection GetScaledBorderAndShadowSelection(){
        if (!AddScaledBorderAndShadow){
            return ScaledBorderAndShadowSelection.DontAdd;
        }

        if (SelectedScaledBorderAndShadow.Content + "" == "ScaledBorderAndShadow: yes"){
            return ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes;
        }

        if (SelectedScaledBorderAndShadow.Content + "" == "ScaledBorderAndShadow: no"){
            return ScaledBorderAndShadowSelection.ScaledBorderAndShadowNo;
        }

        return ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes;
    }

    private ComboBoxItem GetScaledBorderAndShadowFromOptions(CrDownloadOptions options){
        switch (options.SubsAddScaledBorder){
            case (ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes):
                return ScaledBorderAndShadow.FirstOrDefault(a => a.Content != null && (string)a.Content == "ScaledBorderAndShadow: yes") ?? ScaledBorderAndShadow[0];
            case ScaledBorderAndShadowSelection.ScaledBorderAndShadowNo:
                return ScaledBorderAndShadow.FirstOrDefault(a => a.Content != null && (string)a.Content == "ScaledBorderAndShadow: no") ?? ScaledBorderAndShadow[0];
            default:
                return ScaledBorderAndShadow[0];
        }
    }

    [RelayCommand]
    public void AddMkvMergeParam(){
        MkvMergeOptions.Add(new StringItem(){ stringValue = MkvMergeOption });
        MkvMergeOption = "";
        RaisePropertyChanged(nameof(MkvMergeOptions));
    }

    [RelayCommand]
    public void RemoveMkvMergeParam(StringItem param){
        MkvMergeOptions.Remove(param);
        RaisePropertyChanged(nameof(MkvMergeOptions));
    }

    [RelayCommand]
    public void AddFfmpegParam(){
        FfmpegOptions.Add(new StringItem(){ stringValue = FfmpegOption });
        FfmpegOption = "";
        RaisePropertyChanged(nameof(FfmpegOptions));
    }

    [RelayCommand]
    public void RemoveFfmpegParam(StringItem param){
        FfmpegOptions.Remove(param);
        RaisePropertyChanged(nameof(FfmpegOptions));
    }

    private void Changes(object? sender, NotifyCollectionChangedEventArgs e){
        UpdateSettings();

        var dubs = SelectedDubLang.Select(item => item.Content?.ToString());
        SelectedDubs = string.Join(", ", dubs) ?? "";

        var subs = SelectedSubLang.Select(item => item.Content?.ToString());
        SelectedSubs = string.Join(", ", subs) ?? "";
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e){
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(SelectedDubs)
            or nameof(SelectedSubs)
            or nameof(ListBoxColor)){
            return;
        }

        UpdateSettings();

        if (e.PropertyName is nameof(History)){
            if (CrunchyrollManager.Instance.CrunOptions.History){
                if (File.Exists(CfgManager.PathCrHistory)){
                    var decompressedJson = CfgManager.DecompressJsonFile(CfgManager.PathCrHistory);
                    if (!string.IsNullOrEmpty(decompressedJson)){
                        CrunchyrollManager.Instance.HistoryList = Helpers.Deserialize<ObservableCollection<HistorySeries>>(decompressedJson, CrunchyrollManager.Instance.SettingsJsonSerializerSettings) ??
                                                                  new ObservableCollection<HistorySeries>();

                        foreach (var historySeries in CrunchyrollManager.Instance.HistoryList){
                            historySeries.Init();
                            foreach (var historySeriesSeason in historySeries.Seasons){
                                historySeriesSeason.Init();
                            }
                        }
                    } else{
                        CrunchyrollManager.Instance.HistoryList =[];
                    }
                }

                _ = SonarrClient.Instance.RefreshSonarrLite();
            } else{
                CrunchyrollManager.Instance.HistoryList =[];
            }
        }
    }

    [RelayCommand]
    public async Task CreateEncodingPresetButtonPress(bool editMode){
        var dialog = new ContentDialog(){
            Title = "New Encoding Preset",
            PrimaryButtonText = "Save",
            CloseButtonText = "Close",
            FullSizeDesired = true
        };

        var viewModel = new ContentDialogEncodingPresetViewModel(dialog, editMode);
        dialog.Content = new ContentDialogEncodingPresetView(){
            DataContext = viewModel
        };

        var dialogResult = await dialog.ShowAsync();

        if (dialogResult == ContentDialogResult.Primary){
            settingsLoaded = false;
            EncodingPresetsList.Clear();
            foreach (var encodingPreset in FfmpegEncoding.presets){
                EncodingPresetsList.Add(new StringItem{ stringValue = encodingPreset.PresetName ?? "Unknown Preset Name" });
            }

            settingsLoaded = true;
            StringItem? encodingPresetSelected = EncodingPresetsList.FirstOrDefault(a => string.IsNullOrEmpty(a.stringValue) && a.stringValue == CrunchyrollManager.Instance.CrunOptions.EncodingPresetName) ?? null;
            SelectedEncodingPreset = encodingPresetSelected ?? EncodingPresetsList[0];
        }
    }

    private List<StringItemWithDisplayName> GetAvailableHWAccelOptions(){
        try{
            using (var process = new Process()){
                process.StartInfo.FileName = CfgManager.PathFFMPEG;
                process.StartInfo.Arguments = "-hwaccels";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                string output = string.Empty;
                
                process.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        output += e.Data + Environment.NewLine;
                    }
                };

                process.Start();
                
                process.BeginOutputReadLine();
                // process.BeginErrorReadLine();

                process.WaitForExit();

                var lines = output.Split(new[]{ '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var accels = lines.Skip(1).Select(l => l.Trim().ToLower()).ToList();
                return MapHWAccelOptions(accels);
            }
        } catch (Exception e){
            Console.WriteLine("Failed to get Available HW Accel Options" + e);
        }


        return[];
    }

    private List<StringItemWithDisplayName> MapHWAccelOptions(List<string> accels){
        var options = new List<StringItemWithDisplayName>{
            new(){ DisplayName = "CPU Only", value = "" },
            new(){ DisplayName = "Auto", value = "-hwaccel auto " }
        };

        if (accels.Contains("cuda")) options.Add(new StringItemWithDisplayName{ DisplayName = "NVIDIA (CUDA)", value = "-hwaccel cuda " });
        if (accels.Contains("qsv")) options.Add(new StringItemWithDisplayName{ DisplayName = "Intel Quick Sync (QSV)", value = "-hwaccel qsv " });
        if (accels.Contains("dxva2")) options.Add(new StringItemWithDisplayName{ DisplayName = "AMD/Intel DXVA2", value = "-hwaccel dxva2" });
        if (accels.Contains("d3d11va")) options.Add(new StringItemWithDisplayName{ DisplayName = "AMD/Intel D3D11VA", value = "-hwaccel d3d11va " });
        if (accels.Contains("d3d12va")) options.Add(new StringItemWithDisplayName{ DisplayName = "AMD/Intel D3D12VA", value = "-hwaccel d3d12va " });
        if (accels.Contains("vaapi")) options.Add(new StringItemWithDisplayName{ DisplayName = "VAAPI (Linux)", value = "-hwaccel vaapi " });
        if (accels.Contains("videotoolbox")) options.Add(new StringItemWithDisplayName{ DisplayName = "Apple VideoToolbox", value = "-hwaccel videotoolbox " });

        // if (accels.Contains("opencl")) options.Add(new(){DisplayName = "OpenCL (Advanced)", value ="-hwaccel opencl "});
        // if (accels.Contains("vulkan")) options.Add(new(){DisplayName = "Vulkan (Experimental)", value ="-hwaccel vulkan "});

        return options;
    }
}