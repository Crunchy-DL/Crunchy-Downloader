using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader;
using CRD.Utils;
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
    private bool _muxToMp4 = false;

    [ObservableProperty]
    private bool _history = false;

    [ObservableProperty]
    private int _leadingNumbers = 0;

    [ObservableProperty]
    private int _simultaneousDownloads = 0;

    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private string _mkvMergeOptions = "";

    [ObservableProperty]
    private string _ffmpegOptions = "";

    [ObservableProperty]
    private string _selectedSubs = "all";

    [ObservableProperty]
    private ComboBoxItem _selectedHSLang;

    [ObservableProperty]
    private string _selectedDubs = "ja-JP";

    [ObservableProperty]
    private ObservableCollection<ListBoxItem> _selectedDubLang = new();

    [ObservableProperty]
    private ComboBoxItem? _selectedVideoQuality;

    [ObservableProperty]
    private ComboBoxItem? _selectedAudioQuality;

    [ObservableProperty]
    private ComboBoxItem? _currentAppTheme;

    [ObservableProperty]
    private ObservableCollection<ListBoxItem> _selectedSubLang = new();

    [ObservableProperty]
    private bool _useCustomAccent = false;

    [ObservableProperty]
    private Color _listBoxColor;

    [ObservableProperty]
    private Color _customAccentColor = Colors.SlateBlue;

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

    public ObservableCollection<ListBoxItem> SubLangList{ get; } = new(){
        new ListBoxItem(){ Content = "all" },
        new ListBoxItem(){ Content = "none" },
    };

    private readonly FluentAvaloniaTheme _faTheme;

    private bool settingsLoaded = false;

    public SettingsPageViewModel(){
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _currentVersion = $"{version?.Major}.{version?.Minor}.{version?.Build}";


        _faTheme = App.Current.Styles[0] as FluentAvaloniaTheme;

        foreach (var languageItem in Languages.languages){
            HardSubLangList.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
            SubLangList.Add(new ListBoxItem{ Content = languageItem.CrLocale });
            DubLangList.Add(new ComboBoxItem{ Content = languageItem.CrLocale });
        }

        CrDownloadOptions options = Crunchyroll.Instance.CrunOptions;

        var softSubLang = SubLangList.Where(a => options.DlSubs.Contains(a.Content)).ToList();

        SelectedSubLang.Clear();
        foreach (var listBoxItem in softSubLang){
            SelectedSubLang.Add(listBoxItem);
        }

        if (SelectedSubLang.Count == 0){
            SelectedSubs = "none";
        } else{
            SelectedSubs = SelectedSubLang[0].Content.ToString();
            for (var i = 1; i < SelectedSubLang.Count; i++){
                SelectedSubs += "," + SelectedSubLang[i].Content;
            }
        }

        ComboBoxItem? hsLang = HardSubLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.Hslang) ?? null;
        SelectedHSLang = hsLang ?? HardSubLangList[0];

        // ComboBoxItem? dubLang = DubLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.DubLang[0]) ?? null;
        // SelectedDubLang = dubLang ?? DubLangList[0];

        var dubLang = DubLangList.Where(a => options.DubLang.Contains(a.Content)).ToList();

        SelectedDubLang.Clear();
        foreach (var listBoxItem in dubLang){
            SelectedDubLang.Add(listBoxItem);
        }

        if (SelectedDubLang.Count == 0){
            SelectedDubs = "none";
        } else{
            SelectedDubs = SelectedDubLang[0].Content.ToString();
            for (var i = 1; i < SelectedDubLang.Count; i++){
                SelectedDubs += "," + SelectedDubLang[i].Content;
            }
        }


        DownloadVideo = !options.Novids;
        DownloadAudio = !options.Noaudio;
        DownloadChapters = options.Chapters;
        MuxToMp4 = options.Mp4;
        LeadingNumbers = options.Numbers;
        FileName = options.FileName;
        SimultaneousDownloads = options.SimultaneousDownloads;

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

        //TODO - Mux Options

        SelectedSubLang.CollectionChanged += Changes;
        SelectedDubLang.CollectionChanged += Changes;

        settingsLoaded = true;
    }

    private void UpdateSettings(){
        if (!settingsLoaded){
            return;
        }

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

        Crunchyroll.Instance.CrunOptions.Novids = !DownloadVideo;
        Crunchyroll.Instance.CrunOptions.Noaudio = !DownloadAudio;
        Crunchyroll.Instance.CrunOptions.Chapters = DownloadChapters;
        Crunchyroll.Instance.CrunOptions.Mp4 = MuxToMp4;
        Crunchyroll.Instance.CrunOptions.Numbers = LeadingNumbers;
        Crunchyroll.Instance.CrunOptions.FileName = FileName;


        List<string> softSubs = new List<string>();
        foreach (var listBoxItem in SelectedSubLang){
            softSubs.Add(listBoxItem.Content + "");
        }

        Crunchyroll.Instance.CrunOptions.DlSubs = softSubs;

        string hslang = SelectedHSLang.Content + "";

        Crunchyroll.Instance.CrunOptions.Hslang = hslang != "none" ? Languages.FindLang(hslang).Locale : hslang;


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

        //TODO - Mux Options

        CfgManager.WriteSettingsToFile();

        // Console.WriteLine("Updated Settings");
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
}