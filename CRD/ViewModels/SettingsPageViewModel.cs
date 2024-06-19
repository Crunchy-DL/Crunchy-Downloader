using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Utils;
using CRD.Utils.Sonarr;
using CRD.Utils.Structs;
using FluentAvalonia.Styling;

namespace CRD.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase{
    [ObservableProperty]
    private string _currentVersion;

    [ObservableProperty]
    private bool _downloadVideo = true;

    [ObservableProperty]
    private bool _downloadAudio = true;

    [ObservableProperty]
    private bool _downloadChapters = true;
    
    [ObservableProperty]
    private bool _addScaledBorderAndShadow = false;
    
    [ObservableProperty]
    private ComboBoxItem _selectedScaledBorderAndShadow;
    
    public ObservableCollection<ComboBoxItem> ScaledBorderAndShadow{ get; } = new(){
        new ComboBoxItem(){ Content = "ScaledBorderAndShadow: yes" },
        new ComboBoxItem(){ Content = "ScaledBorderAndShadow: no" },
    };

    [ObservableProperty]
    private bool _muxToMp4;
    
    [ObservableProperty]
    private bool _downloadVideoForEveryDub;

    [ObservableProperty]
    private bool _skipSubMux;

    [ObservableProperty]
    private bool _history;
    
    [ObservableProperty]
    private int _leadingNumbers;

    [ObservableProperty]
    private int _simultaneousDownloads;

    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private ObservableCollection<MuxingParam> _mkvMergeOptions = new();

    [ObservableProperty]
    private string _mkvMergeOption = "";

    [ObservableProperty]
    private string _ffmpegOption = "";

    [ObservableProperty]
    private ObservableCollection<MuxingParam> _ffmpegOptions = new();

    [ObservableProperty]
    private string _selectedSubs = "all";

    [ObservableProperty]
    private ComboBoxItem _selectedHSLang;

    [ObservableProperty]
    private string _selectedDubs = "ja-JP";

    [ObservableProperty]
    private ObservableCollection<ListBoxItem> _selectedDubLang = new();

    [ObservableProperty]
    private ComboBoxItem _selectedStreamEndpoint;
    
    [ObservableProperty]
    private ComboBoxItem _selectedDefaultDubLang;

    [ObservableProperty]
    private ComboBoxItem _selectedDefaultSubLang;

    [ObservableProperty]
    private ComboBoxItem? _selectedVideoQuality;

    [ObservableProperty]
    private ComboBoxItem? _selectedAudioQuality;

    [ObservableProperty]
    private ComboBoxItem? _currentAppTheme;

    [ObservableProperty]
    private ObservableCollection<ListBoxItem> _selectedSubLang = new();

    [ObservableProperty]
    private bool _useCustomAccent;

    [ObservableProperty]
    private Color _listBoxColor;

    [ObservableProperty]
    private Color _customAccentColor = Colors.SlateBlue;

    [ObservableProperty]
    private string _sonarrHost = "localhost";

    [ObservableProperty]
    private string _sonarrPort = "8989";

    [ObservableProperty]
    private string _sonarrApiKey = "";

    [ObservableProperty]
    private bool _sonarrUseSsl = false;

    [ObservableProperty]
    private bool _sonarrUseSonarrNumbering = false;

    [ObservableProperty]
    private bool _logMode = false;

    public ObservableCollection<Color> PredefinedColors{ get; } = new(){
        Color.FromRgb(255, 185, 0),
        Color.FromRgb(255, 140, 0),
        Color.FromRgb(247, 99, 12),
        Color.FromRgb(202, 80, 16),
        Color.FromRgb(218, 59, 1),
        Color.FromRgb(239, 105, 80),
        Color.FromRgb(209, 52, 56),
        Color.FromRgb(255, 67, 67),
        Color.FromRgb(231, 72, 86),
        Color.FromRgb(232, 17, 35),
        Color.FromRgb(234, 0, 94),
        Color.FromRgb(195, 0, 82),
        Color.FromRgb(227, 0, 140),
        Color.FromRgb(191, 0, 119),
        Color.FromRgb(194, 57, 179),
        Color.FromRgb(154, 0, 137),
        Color.FromRgb(0, 120, 212),
        Color.FromRgb(0, 99, 177),
        Color.FromRgb(142, 140, 216),
        Color.FromRgb(107, 105, 214),
        Colors.SlateBlue,
        Color.FromRgb(135, 100, 184),
        Color.FromRgb(116, 77, 169),
        Color.FromRgb(177, 70, 194),
        Color.FromRgb(136, 23, 152),
        Color.FromRgb(0, 153, 188),
        Color.FromRgb(45, 125, 154),
        Color.FromRgb(0, 183, 195),
        Color.FromRgb(3, 131, 135),
        Color.FromRgb(0, 178, 148),
        Color.FromRgb(1, 133, 116),
        Color.FromRgb(0, 204, 106),
        Color.FromRgb(16, 137, 62),
        Color.FromRgb(122, 117, 116),
        Color.FromRgb(93, 90, 88),
        Color.FromRgb(104, 118, 138),
        Color.FromRgb(81, 92, 107),
        Color.FromRgb(86, 124, 115),
        Color.FromRgb(72, 104, 96),
        Color.FromRgb(73, 130, 5),
        Color.FromRgb(16, 124, 16),
        Color.FromRgb(118, 118, 118),
        Color.FromRgb(76, 74, 72),
        Color.FromRgb(105, 121, 126),
        Color.FromRgb(74, 84, 89),
        Color.FromRgb(100, 124, 100),
        Color.FromRgb(82, 94, 84),
        Color.FromRgb(132, 117, 69),
        Color.FromRgb(126, 115, 95)
    };

    public ObservableCollection<ComboBoxItem> AppThemes{ get; } = new(){
        new ComboBoxItem(){ Content = "System" },
        new ComboBoxItem(){ Content = "Light" },
        new ComboBoxItem(){ Content = "Dark" },
    };

    public ObservableCollection<ComboBoxItem> VideoQualityList{ get; } = new(){
        new ComboBoxItem(){ Content = "best" },
        new ComboBoxItem(){ Content = "1080" },
        new ComboBoxItem(){ Content = "720" },
        new ComboBoxItem(){ Content = "480" },
        new ComboBoxItem(){ Content = "360" },
        new ComboBoxItem(){ Content = "240" },
        new ComboBoxItem(){ Content = "worst" },
    };

    public ObservableCollection<ComboBoxItem> AudioQualityList{ get; } = new(){
        new ComboBoxItem(){ Content = "best" },
        new ComboBoxItem(){ Content = "128kB/s" },
        new ComboBoxItem(){ Content = "96kB/s" },
        new ComboBoxItem(){ Content = "64kB/s" },
        new ComboBoxItem(){ Content = "worst" },
    };

    public ObservableCollection<ComboBoxItem> HardSubLangList{ get; } = new(){
        new ComboBoxItem(){ Content = "none" },
    };

    public ObservableCollection<ComboBoxItem> DubLangList{ get; } = new(){
    };


    public ObservableCollection<ComboBoxItem> DefaultDubLangList{ get; } = new(){
    };

    public ObservableCollection<ComboBoxItem> DefaultSubLangList{ get; } = new(){
    };


    public ObservableCollection<ListBoxItem> SubLangList{ get; } = new(){
        new ListBoxItem(){ Content = "all" },
        new ListBoxItem(){ Content = "none" },
    };
    
    public ObservableCollection<ComboBoxItem> StreamEndpoints{ get; } = new(){
        new ComboBoxItem(){ Content = "web/firefox" },
        new ComboBoxItem(){ Content = "console/switch" },
        new ComboBoxItem(){ Content = "console/ps4" },
        new ComboBoxItem(){ Content = "console/ps5" },
        new ComboBoxItem(){ Content = "console/xbox_one" },
        new ComboBoxItem(){ Content = "web/edge" },
        // new ComboBoxItem(){ Content = "web/safari" },
        new ComboBoxItem(){ Content = "web/chrome" },
        new ComboBoxItem(){ Content = "web/fallback" },
        // new ComboBoxItem(){ Content = "ios/iphone" },
        // new ComboBoxItem(){ Content = "ios/ipad" },
        new ComboBoxItem(){ Content = "android/phone" },
        new ComboBoxItem(){ Content = "tv/samsung" },
    };

    private readonly FluentAvaloniaTheme _faTheme;

    private bool settingsLoaded;

    public SettingsPageViewModel(){
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _currentVersion = $"{version?.Major}.{version?.Minor}.{version?.Build}";


        _faTheme = App.Current.Styles[0] as FluentAvaloniaTheme;

        foreach (var languageItem in Languages.languages){
            HardSubLangList.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
            SubLangList.Add(new ListBoxItem{ Content = languageItem.CrLocale });
            DubLangList.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
            DefaultDubLangList.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
            DefaultSubLangList.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
        }

        CrDownloadOptions options = Crunchyroll.Instance.CrunOptions;

        ComboBoxItem? hsLang = HardSubLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.Hslang) ?? null;
        SelectedHSLang = hsLang ?? HardSubLangList[0];

        ComboBoxItem? defaultDubLang = DefaultDubLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == (options.DefaultAudio ?? "")) ?? null;
        SelectedDefaultDubLang = defaultDubLang ?? DefaultDubLangList[0];

        ComboBoxItem? defaultSubLang = DefaultSubLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == (options.DefaultSub ?? "")) ?? null;
        SelectedDefaultSubLang = defaultSubLang ?? DefaultSubLangList[0];

        ComboBoxItem? streamEndpoint = StreamEndpoints.FirstOrDefault(a => a.Content != null && (string)a.Content == (options.StreamEndpoint ?? "")) ?? null;
        SelectedStreamEndpoint = streamEndpoint ?? StreamEndpoints[0];
        
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

        UpdateSubAndDubString();

        var props = options.SonarrProperties;

        if (props != null){
            SonarrUseSsl = props.UseSsl;
            SonarrUseSonarrNumbering = props.UseSonarrNumbering;
            SonarrHost = props.Host + "";
            SonarrPort = props.Port + "";
            SonarrApiKey = props.ApiKey + "";
        }

        AddScaledBorderAndShadow = options.SubsAddScaledBorder is ScaledBorderAndShadowSelection.ScaledBorderAndShadowNo or ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes;
        SelectedScaledBorderAndShadow = GetScaledBorderAndShadowFromOptions(options);
        
        DownloadVideo = !options.Novids;
        DownloadAudio = !options.Noaudio;
        DownloadVideoForEveryDub = !options.DlVideoOnce;
        DownloadChapters = options.Chapters;
        MuxToMp4 = options.Mp4;
        SkipSubMux = options.SkipSubsMux;
        LeadingNumbers = options.Numbers;
        FileName = options.FileName;
        SimultaneousDownloads = options.SimultaneousDownloads;
        LogMode = options.LogMode;

        ComboBoxItem? qualityAudio = AudioQualityList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.QualityAudio) ?? null;
        SelectedAudioQuality = qualityAudio ?? AudioQualityList[0];

        ComboBoxItem? qualityVideo = VideoQualityList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.QualityVideo) ?? null;
        SelectedVideoQuality = qualityVideo ?? VideoQualityList[0];

        ComboBoxItem? theme = AppThemes.FirstOrDefault(a => a.Content != null && (string)a.Content == options.Theme) ?? null;
        CurrentAppTheme = theme ?? AppThemes[0];

        if (options.AccentColor != CustomAccentColor.ToString()){
            UseCustomAccent = true;
        }

        History = options.History;

        MkvMergeOptions.Clear();
        if (options.MkvmergeOptions != null){
            foreach (var mkvmergeParam in options.MkvmergeOptions){
                MkvMergeOptions.Add(new MuxingParam(){ ParamValue = mkvmergeParam });
            }
        }

        FfmpegOptions.Clear();
        if (options.FfmpegOptions != null){
            foreach (var ffmpegParam in options.FfmpegOptions){
                FfmpegOptions.Add(new MuxingParam(){ ParamValue = ffmpegParam });
            }
        }

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

        UpdateSubAndDubString();

        Crunchyroll.Instance.CrunOptions.Novids = !DownloadVideo;
        Crunchyroll.Instance.CrunOptions.Noaudio = !DownloadAudio;
        Crunchyroll.Instance.CrunOptions.DlVideoOnce = !DownloadVideoForEveryDub;
        Crunchyroll.Instance.CrunOptions.Chapters = DownloadChapters;
        Crunchyroll.Instance.CrunOptions.Mp4 = MuxToMp4;
        Crunchyroll.Instance.CrunOptions.SkipSubsMux = SkipSubMux;
        Crunchyroll.Instance.CrunOptions.Numbers = LeadingNumbers;
        Crunchyroll.Instance.CrunOptions.FileName = FileName;

        Crunchyroll.Instance.CrunOptions.SubsAddScaledBorder = GetScaledBorderAndShadowSelection();

        List<string> softSubs = new List<string>();
        foreach (var listBoxItem in SelectedSubLang){
            softSubs.Add(listBoxItem.Content + "");
        }

        Crunchyroll.Instance.CrunOptions.DlSubs = softSubs;

        string hslang = SelectedHSLang.Content + "";

        Crunchyroll.Instance.CrunOptions.Hslang = hslang != "none" ? Languages.FindLang(hslang).Locale : hslang;

        Crunchyroll.Instance.CrunOptions.DefaultAudio = SelectedDefaultDubLang.Content + "";
        Crunchyroll.Instance.CrunOptions.DefaultSub = SelectedDefaultSubLang.Content + "";
        
        
        Crunchyroll.Instance.CrunOptions.StreamEndpoint = SelectedStreamEndpoint.Content + "";

        List<string> dubLangs = new List<string>();
        foreach (var listBoxItem in SelectedDubLang){
            dubLangs.Add(listBoxItem.Content + "");
        }

        Crunchyroll.Instance.CrunOptions.DubLang = dubLangs;


        Crunchyroll.Instance.CrunOptions.SimultaneousDownloads = SimultaneousDownloads;
        
        Crunchyroll.Instance.CrunOptions.QualityAudio = SelectedAudioQuality?.Content + "";
        Crunchyroll.Instance.CrunOptions.QualityVideo = SelectedVideoQuality?.Content + "";
        Crunchyroll.Instance.CrunOptions.Theme = CurrentAppTheme?.Content + "";

        Crunchyroll.Instance.CrunOptions.AccentColor = _faTheme.CustomAccentColor.ToString();

        Crunchyroll.Instance.CrunOptions.History = History;

        var props = new SonarrProperties();

        props.UseSsl = SonarrUseSsl;
        props.UseSonarrNumbering = SonarrUseSonarrNumbering;
        props.Host = SonarrHost;

        if (int.TryParse(SonarrPort, out var portNumber)){
            props.Port = portNumber;
        } else{
            props.Port = 8989;
        }

        props.ApiKey = SonarrApiKey;


        Crunchyroll.Instance.CrunOptions.SonarrProperties = props;

        Crunchyroll.Instance.CrunOptions.LogMode = LogMode;

        List<string> mkvmergeParams = new List<string>();
        foreach (var mkvmergeParam in MkvMergeOptions){
            mkvmergeParams.Add(mkvmergeParam.ParamValue);
        }

        Crunchyroll.Instance.CrunOptions.MkvmergeOptions = mkvmergeParams;

        List<string> ffmpegParams = new List<string>();
        foreach (var ffmpegParam in FfmpegOptions){
            ffmpegParams.Add(ffmpegParam.ParamValue);
        }

        Crunchyroll.Instance.CrunOptions.FfmpegOptions = ffmpegParams;

        CfgManager.WriteSettingsToFile();
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
    
    
    private void UpdateSubAndDubString(){
        if (SelectedSubLang.Count == 0){
            SelectedSubs = "none";
        } else{
            SelectedSubs = SelectedSubLang[0].Content.ToString();
            for (var i = 1; i < SelectedSubLang.Count; i++){
                SelectedSubs += "," + SelectedSubLang[i].Content;
            }
        }

        if (SelectedDubLang.Count == 0){
            SelectedDubs = "none";
        } else{
            SelectedDubs = SelectedDubLang[0].Content.ToString();
            for (var i = 1; i < SelectedDubLang.Count; i++){
                SelectedDubs += "," + SelectedDubLang[i].Content;
            }
        }
    }

    [RelayCommand]
    public void AddMkvMergeParam(){
        MkvMergeOptions.Add(new MuxingParam(){ ParamValue = MkvMergeOption });
        MkvMergeOption = "";
        RaisePropertyChanged(nameof(MkvMergeOptions));
    }

    [RelayCommand]
    public void RemoveMkvMergeParam(MuxingParam param){
        MkvMergeOptions.Remove(param);
        RaisePropertyChanged(nameof(MkvMergeOptions));
    }

    [RelayCommand]
    public void AddFfmpegParam(){
        FfmpegOptions.Add(new MuxingParam(){ ParamValue = FfmpegOption });
        FfmpegOption = "";
        RaisePropertyChanged(nameof(FfmpegOptions));
    }

    [RelayCommand]
    public void RemoveFfmpegParam(MuxingParam param){
        FfmpegOptions.Remove(param);
        RaisePropertyChanged(nameof(FfmpegOptions));
    }

    partial void OnCurrentAppThemeChanged(ComboBoxItem? value){
        if (value?.Content?.ToString() == "System"){
            _faTheme.PreferSystemTheme = true;
        } else if (value?.Content?.ToString() == "Dark"){
            _faTheme.PreferSystemTheme = false;
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        } else{
            _faTheme.PreferSystemTheme = false;
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        }

        UpdateSettings();
    }

    partial void OnUseCustomAccentChanged(bool value){
        if (value){
            if (_faTheme.TryGetResource("SystemAccentColor", null, out var curColor)){
                CustomAccentColor = (Color)curColor;
                ListBoxColor = CustomAccentColor;

                RaisePropertyChanged(nameof(CustomAccentColor));
                RaisePropertyChanged(nameof(ListBoxColor));
            }
        } else{
            CustomAccentColor = default;
            ListBoxColor = default;
            UpdateAppAccentColor(Colors.SlateBlue);
        }
    }

    partial void OnListBoxColorChanged(Color value){
        if (value != null){
            CustomAccentColor = value;
            RaisePropertyChanged(nameof(CustomAccentColor));

            UpdateAppAccentColor(value);
        }
    }

    partial void OnCustomAccentColorChanged(Color value){
        ListBoxColor = value;
        RaisePropertyChanged(nameof(ListBoxColor));
        UpdateAppAccentColor(value);
    }

    private void UpdateAppAccentColor(Color? color){
        _faTheme.CustomAccentColor = color;
        UpdateSettings();
    }

    private void Changes(object? sender, NotifyCollectionChangedEventArgs e){
        UpdateSettings();
    }

    partial void OnDownloadAudioChanged(bool value){
        UpdateSettings();
    }

    partial void OnDownloadChaptersChanged(bool value){
        UpdateSettings();
    }

    partial void OnDownloadVideoChanged(bool value){
        UpdateSettings();
    }

    partial void OnFileNameChanged(string value){
        UpdateSettings();
    }

    partial void OnLeadingNumbersChanged(int value){
        UpdateSettings();
    }

    partial void OnMuxToMp4Changed(bool value){
        UpdateSettings();
    }

    partial void OnSelectedHSLangChanged(ComboBoxItem value){
        UpdateSettings();
    }

    partial void OnSimultaneousDownloadsChanged(int value){
        UpdateSettings();
    }

    partial void OnSelectedAudioQualityChanged(ComboBoxItem? value){
        UpdateSettings();
    }

    partial void OnSelectedVideoQualityChanged(ComboBoxItem? value){
        UpdateSettings();
    }
    
    partial void OnHistoryChanged(bool value){
        UpdateSettings();
    }

    partial void OnSonarrHostChanged(string value){
        UpdateSettings();
    }

    partial void OnSonarrPortChanged(string value){
        UpdateSettings();
    }

    partial void OnSonarrApiKeyChanged(string value){
        UpdateSettings();
    }

    partial void OnSonarrUseSslChanged(bool value){
        UpdateSettings();
    }

    partial void OnSonarrUseSonarrNumberingChanged(bool value){
        UpdateSettings();
    }

    partial void OnLogModeChanged(bool value){
        UpdateSettings();
        if (value){
            CfgManager.EnableLogMode();
        } else{
            CfgManager.DisableLogMode();
        }
    }

    partial void OnSelectedDefaultDubLangChanged(ComboBoxItem value){
        UpdateSettings();
    }

    partial void OnSelectedDefaultSubLangChanged(ComboBoxItem value){
        UpdateSettings();
    }

    partial void OnSkipSubMuxChanged(bool value){
        UpdateSettings();
    }

    partial void OnDownloadVideoForEveryDubChanged(bool value){
        UpdateSettings();
    }

    partial void OnSelectedStreamEndpointChanged(ComboBoxItem value){
        UpdateSettings();
    }

    partial void OnAddScaledBorderAndShadowChanged(bool value){
        UpdateSettings();
    }

    partial void OnSelectedScaledBorderAndShadowChanged(ComboBoxItem value){
        UpdateSettings();
    }
}

public class MuxingParam{
    public string ParamValue{ get; set; }
}