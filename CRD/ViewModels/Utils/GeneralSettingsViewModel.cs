using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Sonarr;
using CRD.Utils.Structs.Crunchyroll;
using CRD.Utils.Structs.History;
using FluentAvalonia.Styling;

namespace CRD.ViewModels.Utils;

// ReSharper disable InconsistentNaming
public partial class GeneralSettingsViewModel : ViewModelBase{
    [ObservableProperty]
    private string _currentVersion;

    [ObservableProperty]
    private bool _downloadToTempFolder;

    [ObservableProperty]
    private bool _history;

    [ObservableProperty]
    private bool _historyIncludeCrArtists;
    
    [ObservableProperty]
    private bool _historyAddSpecials;
    
    [ObservableProperty]
    private bool _historySkipUnmonitored;

    [ObservableProperty]
    private bool _historyCountSonarr;

    [ObservableProperty]
    private double? _simultaneousDownloads;

    [ObservableProperty]
    private double? _downloadSpeed;

    [ObservableProperty]
    private ComboBoxItem _selectedHistoryLang;

    [ObservableProperty]
    private ComboBoxItem? _currentAppTheme;

    [ObservableProperty]
    private bool _useCustomAccent;

    [ObservableProperty]
    private string _backgroundImagePath;

    [ObservableProperty]
    private double? _backgroundImageOpacity;

    [ObservableProperty]
    private double? _backgroundImageBlurRadius;

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

    public ObservableCollection<ComboBoxItem> HistoryLangList{ get; } = new(){
        new ComboBoxItem(){ Content = "default" },
        new ComboBoxItem(){ Content = "de-DE" },
        new ComboBoxItem(){ Content = "en-US" },
        new ComboBoxItem(){ Content = "es-419" },
        new ComboBoxItem(){ Content = "es-ES" },
        new ComboBoxItem(){ Content = "fr-FR" },
        new ComboBoxItem(){ Content = "it-IT" },
        new ComboBoxItem(){ Content = "pt-BR" },
        new ComboBoxItem(){ Content = "pt-PT" },
        new ComboBoxItem(){ Content = "ru-RU" },
        new ComboBoxItem(){ Content = "hi-IN" },
        new ComboBoxItem(){ Content = "ar-SA" },
    };

    [ObservableProperty]
    private string _downloadDirPath;

    [ObservableProperty]
    private bool _proxyEnabled;

    [ObservableProperty]
    private bool _proxySocks;

    [ObservableProperty]
    private string _proxyHost;

    [ObservableProperty]
    private double? _proxyPort;

    [ObservableProperty]
    private string _proxyUsername;

    [ObservableProperty]
    private string _proxyPassword;

    [ObservableProperty]
    private string _tempDownloadDirPath;

    [ObservableProperty]
    private string _currentIp = "";

    private readonly FluentAvaloniaTheme _faTheme;

    private bool settingsLoaded;

    private IStorageProvider _storageProvider;

    public GeneralSettingsViewModel(){
        _storageProvider = ProgramManager.Instance.StorageProvider ?? throw new ArgumentNullException(nameof(ProgramManager.Instance.StorageProvider));

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _currentVersion = $"{version?.Major}.{version?.Minor}.{version?.Build}";

        _faTheme = App.Current.Styles[0] as FluentAvaloniaTheme;

        if (CrunchyrollManager.Instance.CrunOptions.AccentColor != null && !string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.AccentColor)){
            CustomAccentColor = Color.Parse(CrunchyrollManager.Instance.CrunOptions.AccentColor);
        } else{
            CustomAccentColor = Application.Current?.PlatformSettings?.GetColorValues().AccentColor1 ?? Colors.SlateBlue;
        }

        CrDownloadOptions options = CrunchyrollManager.Instance.CrunOptions;

        BackgroundImageBlurRadius = options.BackgroundImageBlurRadius;
        BackgroundImageOpacity = options.BackgroundImageOpacity;
        BackgroundImagePath = options.BackgroundImagePath ?? string.Empty;
        DownloadDirPath = string.IsNullOrEmpty(options.DownloadDirPath) ? CfgManager.PathVIDEOS_DIR : options.DownloadDirPath;
        TempDownloadDirPath = string.IsNullOrEmpty(options.DownloadTempDirPath) ? CfgManager.PathTEMP_DIR : options.DownloadTempDirPath;

        ComboBoxItem? historyLang = HistoryLangList.FirstOrDefault(a => a.Content != null && (string)a.Content == options.HistoryLang) ?? null;
        SelectedHistoryLang = historyLang ?? HistoryLangList[0];

        var props = options.SonarrProperties;

        if (props != null){
            SonarrUseSsl = props.UseSsl;
            SonarrUseSonarrNumbering = props.UseSonarrNumbering;
            SonarrHost = props.Host + "";
            SonarrPort = props.Port + "";
            SonarrApiKey = props.ApiKey + "";
        }

        ProxyEnabled = options.ProxyEnabled;
        ProxySocks = options.ProxySocks;
        ProxyHost = options.ProxyHost ?? "";
        ProxyUsername = options.ProxyUsername ?? "";
        ProxyPassword = options.ProxyPassword ?? "";
        ProxyPort = options.ProxyPort;
        HistoryIncludeCrArtists = options.HistoryIncludeCrArtists;
        HistoryAddSpecials = options.HistoryAddSpecials;
        HistorySkipUnmonitored = options.HistorySkipUnmonitored;
        HistoryCountSonarr = options.HistoryCountSonarr;
        DownloadSpeed = options.DownloadSpeedLimit;
        DownloadToTempFolder = options.DownloadToTempFolder;
        SimultaneousDownloads = options.SimultaneousDownloads;
        LogMode = options.LogMode;

        ComboBoxItem? theme = AppThemes.FirstOrDefault(a => a.Content != null && (string)a.Content == options.Theme) ?? null;
        CurrentAppTheme = theme ?? AppThemes[0];

        if (!string.IsNullOrEmpty(options.AccentColor) && options.AccentColor != Application.Current?.PlatformSettings?.GetColorValues().AccentColor1.ToString()){
            UseCustomAccent = true;
        }

        History = options.History;

        settingsLoaded = true;
    }

    private void UpdateSettings(){
        if (!settingsLoaded){
            return;
        }

        CrunchyrollManager.Instance.CrunOptions.BackgroundImageBlurRadius = Math.Clamp((BackgroundImageBlurRadius ?? 0), 0, 40);
        CrunchyrollManager.Instance.CrunOptions.BackgroundImageOpacity = Math.Clamp((BackgroundImageOpacity ?? 0), 0, 1);

        CrunchyrollManager.Instance.CrunOptions.DownloadToTempFolder = DownloadToTempFolder;
        CrunchyrollManager.Instance.CrunOptions.HistoryAddSpecials = HistoryAddSpecials;
        CrunchyrollManager.Instance.CrunOptions.HistoryIncludeCrArtists = HistoryIncludeCrArtists;
        CrunchyrollManager.Instance.CrunOptions.HistorySkipUnmonitored = HistorySkipUnmonitored;
        CrunchyrollManager.Instance.CrunOptions.HistoryCountSonarr = HistoryCountSonarr;
        CrunchyrollManager.Instance.CrunOptions.DownloadSpeedLimit = Math.Clamp((int)(DownloadSpeed ?? 0), 0, 1000000000);
        CrunchyrollManager.Instance.CrunOptions.SimultaneousDownloads = Math.Clamp((int)(SimultaneousDownloads ?? 0), 1, 10);

        CrunchyrollManager.Instance.CrunOptions.ProxyEnabled = ProxyEnabled;
        CrunchyrollManager.Instance.CrunOptions.ProxySocks = ProxySocks;
        CrunchyrollManager.Instance.CrunOptions.ProxyHost = ProxyHost;
        CrunchyrollManager.Instance.CrunOptions.ProxyPort = Math.Clamp((int)(ProxyPort ?? 0), 0, 65535);
        CrunchyrollManager.Instance.CrunOptions.ProxyUsername = ProxyUsername;
        CrunchyrollManager.Instance.CrunOptions.ProxyPassword = ProxyPassword;

        string historyLang = SelectedHistoryLang.Content + "";

        CrunchyrollManager.Instance.CrunOptions.HistoryLang = historyLang != "default" ? historyLang : CrunchyrollManager.Instance.DefaultLocale;

        CrunchyrollManager.Instance.CrunOptions.Theme = CurrentAppTheme?.Content + "";

        if (_faTheme.CustomAccentColor != (Application.Current?.PlatformSettings?.GetColorValues().AccentColor1)){
            CrunchyrollManager.Instance.CrunOptions.AccentColor = _faTheme.CustomAccentColor.ToString();
        } else{
            CrunchyrollManager.Instance.CrunOptions.AccentColor = string.Empty;
        }

        CrunchyrollManager.Instance.CrunOptions.History = History;

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


        CrunchyrollManager.Instance.CrunOptions.SonarrProperties = props;

        CrunchyrollManager.Instance.CrunOptions.LogMode = LogMode;

        CfgManager.WriteCrSettings();
    }

    [RelayCommand]
    public void ClearDownloadDirPath(){
        CrunchyrollManager.Instance.CrunOptions.DownloadDirPath = string.Empty;
        DownloadDirPath = CfgManager.PathVIDEOS_DIR;
    }

    [RelayCommand]
    public void ClearDownloadTempDirPath(){
        CrunchyrollManager.Instance.CrunOptions.DownloadTempDirPath = string.Empty;
        TempDownloadDirPath = CfgManager.PathTEMP_DIR;
    }

    [RelayCommand]
    public async Task OpenFolderDialogAsync(){
        await OpenFolderDialogAsyncInternal(
            pathSetter: (path) => {
                CrunchyrollManager.Instance.CrunOptions.DownloadDirPath = path;
                DownloadDirPath = string.IsNullOrEmpty(path) ? CfgManager.PathVIDEOS_DIR : path;
            },
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.DownloadDirPath,
            defaultPath: CfgManager.PathVIDEOS_DIR
        );
    }

    [RelayCommand]
    public async Task OpenFolderDialogTempFolderAsync(){
        await OpenFolderDialogAsyncInternal(
            pathSetter: (path) => {
                CrunchyrollManager.Instance.CrunOptions.DownloadTempDirPath = path;
                TempDownloadDirPath = string.IsNullOrEmpty(path) ? CfgManager.PathTEMP_DIR : path;
            },
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.DownloadTempDirPath,
            defaultPath: CfgManager.PathTEMP_DIR
        );
    }

    private async Task OpenFolderDialogAsyncInternal(Action<string> pathSetter, Func<string> pathGetter, string defaultPath){
        if (_storageProvider == null){
            Console.Error.WriteLine("StorageProvider must be set before using the dialog.");
            throw new InvalidOperationException("StorageProvider must be set before using the dialog.");
        }

        var result = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions{
            Title = "Select Folder"
        });

        if (result.Count > 0){
            var selectedFolder = result[0];
            Console.WriteLine($"Selected folder: {selectedFolder.Path.LocalPath}");
            pathSetter(selectedFolder.Path.LocalPath);
            var finalPath = string.IsNullOrEmpty(pathGetter()) ? defaultPath : pathGetter();
            pathSetter(finalPath);
            CfgManager.WriteCrSettings();
        }
    }

    [RelayCommand]
    public void ClearBackgroundImagePath(){
        CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath = string.Empty;
        BackgroundImagePath = string.Empty;
        Helpers.SetBackgroundImage(string.Empty);
    }

    [RelayCommand]
    public async Task OpenImageFileDialogAsyncInternalBackgroundImage(){
        await OpenImageFileDialogAsyncInternal(
            pathSetter: (path) => {
                CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath = path;
                BackgroundImagePath = path;
                Helpers.SetBackgroundImage(path, BackgroundImageOpacity, BackgroundImageBlurRadius);
            },
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath,
            defaultPath: string.Empty
        );
    }

    private async Task OpenImageFileDialogAsyncInternal(Action<string> pathSetter, Func<string> pathGetter, string defaultPath){
        if (_storageProvider == null){
            Console.Error.WriteLine("StorageProvider must be set before using the dialog.");
            throw new InvalidOperationException("StorageProvider must be set before using the dialog.");
        }

        // Open the file picker dialog with only image file types allowed
        var result = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{
            Title = "Select Image File",
            FileTypeFilter = new List<FilePickerFileType>{
                new FilePickerFileType("Image Files"){
                    Patterns = new[]{ "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" }
                }
            },
            AllowMultiple = false
        });

        if (result.Count > 0){
            var selectedFile = result[0];
            Console.WriteLine($"Selected file: {selectedFile.Path.LocalPath}");
            pathSetter(selectedFile.Path.LocalPath);
            var finalPath = string.IsNullOrEmpty(pathGetter()) ? defaultPath : pathGetter();
            pathSetter(finalPath);
            CfgManager.WriteCrSettings();
        }
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
            var color = Application.Current?.PlatformSettings?.GetColorValues().AccentColor1 ?? Colors.SlateBlue;
            UpdateAppAccentColor(color);
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

    protected override void OnPropertyChanged(PropertyChangedEventArgs e){
        base.OnPropertyChanged(e);

        if (e.PropertyName is
            nameof(CustomAccentColor)
            or nameof(ListBoxColor)
            or nameof(CurrentAppTheme)
            or nameof(UseCustomAccent)
            or nameof(LogMode)){
            return;
        }

        UpdateSettings();

        if (e.PropertyName is nameof(History)){
            if (CrunchyrollManager.Instance.CrunOptions.History){
                if (File.Exists(CfgManager.PathCrHistory)){
                    var decompressedJson = CfgManager.DecompressJsonFile(CfgManager.PathCrHistory);

                    if (!string.IsNullOrEmpty(decompressedJson)){
                        var historyList = Helpers.Deserialize<ObservableCollection<HistorySeries>>(
                            decompressedJson,
                            CrunchyrollManager.Instance.SettingsJsonSerializerSettings
                        ) ?? new ObservableCollection<HistorySeries>();

                        CrunchyrollManager.Instance.HistoryList = historyList;

                        Parallel.ForEach(historyList, historySeries => {
                            historySeries.Init();

                            foreach (var historySeriesSeason in historySeries.Seasons){
                                historySeriesSeason.Init();
                            }
                        });
                    } else{
                        CrunchyrollManager.Instance.HistoryList = new ObservableCollection<HistorySeries>();
                    }
                } else{
                    CrunchyrollManager.Instance.HistoryList = new ObservableCollection<HistorySeries>();
                }

                _ = Task.Run(() => SonarrClient.Instance.RefreshSonarrLite());
            } else{
                CrunchyrollManager.Instance.HistoryList = new ObservableCollection<HistorySeries>();
            }
        }

        if (!string.IsNullOrEmpty(BackgroundImagePath) && e.PropertyName is nameof(BackgroundImageBlurRadius) or nameof(BackgroundImageOpacity)){
            Helpers.SetBackgroundImage(BackgroundImagePath, BackgroundImageOpacity, BackgroundImageBlurRadius);
        }
    }

    [RelayCommand]
    public async void CheckIp(){
        var result = await HttpClientReq.Instance.SendHttpRequest(HttpClientReq.CreateRequestMessage("https://icanhazip.com", HttpMethod.Get, false, false, null));
        Console.Error.WriteLine("Your IP: " + result.ResponseContent);
        if (result.IsOk){
            CurrentIp = result.ResponseContent;
        }
    }

    partial void OnLogModeChanged(bool value){
        UpdateSettings();
        if (value){
            CfgManager.EnableLogMode();
        } else{
            CfgManager.DisableLogMode();
        }
    }
}