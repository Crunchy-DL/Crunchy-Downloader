using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll;
using CRD.Utils.Structs.History;
using FluentAvalonia.Styling;

namespace CRD.ViewModels.Utils;

public partial class GeneralSettingsViewModel : ViewModelBase{
    [ObservableProperty]
    private string currentVersion;

    [ObservableProperty]
    private bool downloadToTempFolder;

    [ObservableProperty]
    private bool history;

    [ObservableProperty]
    private bool historyCountMissing;

    [ObservableProperty]
    private bool historyIncludeCrArtists;

    [ObservableProperty]
    private bool historyAddSpecials;

    [ObservableProperty]
    private bool historySkipUnmonitored;

    [ObservableProperty]
    private bool historyCountSonarr;
    
    [ObservableProperty]
    private double? historyAutoRefreshIntervalMinutes;

    [ObservableProperty]
    private HistoryRefreshMode historyAutoRefreshMode;

    [ObservableProperty]
    private string historyAutoRefreshModeHint;
    
    [ObservableProperty]
    private string historyAutoRefreshLastRunTime;

    public ObservableCollection<RefreshModeOption> HistoryAutoRefreshModes{ get; } = new(){
        new RefreshModeOption{ DisplayName = "Default All", value = HistoryRefreshMode.DefaultAll },
        new RefreshModeOption{ DisplayName = "Default Active", value = HistoryRefreshMode.DefaultActive },
        new RefreshModeOption{ DisplayName = "Fast New Releases", value = HistoryRefreshMode.FastNewReleases },
    };

    [ObservableProperty]
    private double? simultaneousDownloads;

    [ObservableProperty]
    private double? simultaneousProcessingJobs;

    [ObservableProperty]
    private bool downloadMethodeNew;
    
    [ObservableProperty]
    private bool downloadOnlyWithAllSelectedDubSub;

    [ObservableProperty]
    private bool downloadAllowEarlyStart;

    [ObservableProperty]
    private double? downloadSpeed;
    
    [ObservableProperty]
    private bool downloadSpeedInBits;

    [ObservableProperty]
    private double? retryAttempts;

    [ObservableProperty]
    private double? retryDelay;
    
    [ObservableProperty]
    private bool trayIconEnabled;
    
    [ObservableProperty]
    private bool startMinimizedToTray;
    
    [ObservableProperty]
    private bool minimizeToTray;
    
    [ObservableProperty]
    private bool minimizeToTrayOnClose;

    [ObservableProperty]
    private ComboBoxItem selectedHistoryLang;

    [ObservableProperty]
    private ComboBoxItem? currentAppTheme;

    [ObservableProperty]
    private bool useCustomAccent;

    [ObservableProperty]
    private string backgroundImagePath;

    [ObservableProperty]
    private double? backgroundImageOpacity;

    [ObservableProperty]
    private double? backgroundImageBlurRadius;

    [ObservableProperty]
    private Color listBoxColor;

    [ObservableProperty]
    private Color customAccentColor = Colors.SlateBlue;

    [ObservableProperty]
    private string sonarrHost = "localhost";

    [ObservableProperty]
    private string sonarrPort = "8989";

    [ObservableProperty]
    private string sonarrApiKey = "";

    [ObservableProperty]
    private bool sonarrUseSsl;

    [ObservableProperty]
    private bool sonarrUseSonarrNumbering;

    [ObservableProperty]
    private bool logMode;

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
        new ComboBoxItem{ Content = "System" },
        new ComboBoxItem{ Content = "Light" },
        new ComboBoxItem{ Content = "Dark" },
    };

    public ObservableCollection<ComboBoxItem> HistoryLangList{ get; } = new(){
        new ComboBoxItem{ Content = "default" },
        new ComboBoxItem{ Content = "de-DE" },
        new ComboBoxItem{ Content = "en-US" },
        new ComboBoxItem{ Content = "es-419" },
        new ComboBoxItem{ Content = "es-ES" },
        new ComboBoxItem{ Content = "fr-FR" },
        new ComboBoxItem{ Content = "it-IT" },
        new ComboBoxItem{ Content = "pt-BR" },
        new ComboBoxItem{ Content = "pt-PT" },
        new ComboBoxItem{ Content = "ru-RU" },
        new ComboBoxItem{ Content = "hi-IN" },
        new ComboBoxItem{ Content = "ar-SA" },
    };

    [ObservableProperty]
    private string downloadDirPath;

    [ObservableProperty]
    private bool proxyEnabled;

    [ObservableProperty]
    private bool proxySocks;

    [ObservableProperty]
    private string proxyHost;

    [ObservableProperty]
    private double? proxyPort;

    [ObservableProperty]
    private string proxyUsername;

    [ObservableProperty]
    private string proxyPassword;
    
    [ObservableProperty]
    private string flareSolverrHost = "localhost";

    [ObservableProperty]
    private string flareSolverrPort = "8191";

    [ObservableProperty]
    private bool flareSolverrUseSsl;

    [ObservableProperty]
    private bool useFlareSolverr;

    [ObservableProperty]
    private string tempDownloadDirPath;

    [ObservableProperty]
    private bool downloadFinishedPlaySound;

    [ObservableProperty]
    private string downloadFinishedSoundPath;
    
    [ObservableProperty]
    private bool downloadFinishedExecute;

    [ObservableProperty]
    private string downloadFinishedExecutePath;

    [ObservableProperty]
    private string currentIp = "";

    private readonly FluentAvaloniaTheme _faTheme;

    private bool settingsLoaded;

    private IStorageProvider _storageProvider;

    public GeneralSettingsViewModel(){
        _storageProvider = ProgramManager.Instance.StorageProvider ?? throw new ArgumentNullException(nameof(ProgramManager.Instance.StorageProvider));

        var version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0];
        CurrentVersion = $"v{version}";

        _faTheme = Application.Current?.Styles[0] as FluentAvaloniaTheme ??[];

        if (CrunchyrollManager.Instance.CrunOptions.AccentColor != null && !string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.AccentColor)){
            CustomAccentColor = Color.Parse(CrunchyrollManager.Instance.CrunOptions.AccentColor);
        } else{
            CustomAccentColor = Application.Current?.PlatformSettings?.GetColorValues().AccentColor1 ?? Colors.SlateBlue;
        }

        CrDownloadOptions options = CrunchyrollManager.Instance.CrunOptions;

        BackgroundImageBlurRadius = options.BackgroundImageBlurRadius;
        BackgroundImageOpacity = options.BackgroundImageOpacity;
        BackgroundImagePath = options.BackgroundImagePath ?? string.Empty;

        DownloadFinishedSoundPath = options.DownloadFinishedSoundPath ?? string.Empty;
        DownloadFinishedPlaySound = options.DownloadFinishedPlaySound;
        
        DownloadFinishedExecutePath = options.DownloadFinishedExecutePath ?? string.Empty;
        DownloadFinishedExecute = options.DownloadFinishedExecute;

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
        
        var propsFlareSolverr = options.FlareSolverrProperties;

        if (propsFlareSolverr != null){
            FlareSolverrUseSsl = propsFlareSolverr.UseSsl;
            UseFlareSolverr = propsFlareSolverr.UseFlareSolverr;
            FlareSolverrHost = propsFlareSolverr.Host + "";
            FlareSolverrPort = propsFlareSolverr.Port + "";
        }

        ProxyEnabled = options.ProxyEnabled;
        ProxySocks = options.ProxySocks;
        ProxyHost = options.ProxyHost ?? "";
        ProxyUsername = options.ProxyUsername ?? "";
        ProxyPassword = options.ProxyPassword ?? "";
        ProxyPort = options.ProxyPort;
        HistoryCountMissing = options.HistoryCountMissing;
        HistoryIncludeCrArtists = options.HistoryIncludeCrArtists;
        HistoryAddSpecials = options.HistoryAddSpecials;
        HistorySkipUnmonitored = options.HistorySkipUnmonitored;
        HistoryCountSonarr = options.HistoryCountSonarr;
        HistoryAutoRefreshIntervalMinutes = options.HistoryAutoRefreshIntervalMinutes;
        HistoryAutoRefreshMode = options.HistoryAutoRefreshMode;
        HistoryAutoRefreshLastRunTime = ProgramManager.Instance.GetLastRefreshTime() == DateTime.MinValue ? "Never" : ProgramManager.Instance.GetLastRefreshTime().ToString("g", CultureInfo.CurrentCulture);
        DownloadSpeed = options.DownloadSpeedLimit;
        DownloadSpeedInBits = options.DownloadSpeedInBits;
        DownloadMethodeNew = options.DownloadMethodeNew;
        DownloadAllowEarlyStart = options.DownloadAllowEarlyStart;
        DownloadOnlyWithAllSelectedDubSub = options.DownloadOnlyWithAllSelectedDubSub;
        RetryAttempts = Math.Clamp((options.RetryAttempts), 1, 10);
        RetryDelay = Math.Clamp((options.RetryDelay), 1, 30);
        DownloadToTempFolder = options.DownloadToTempFolder;
        SimultaneousDownloads = options.SimultaneousDownloads;
        SimultaneousProcessingJobs = options.SimultaneousProcessingJobs;
        LogMode = options.LogMode;
        
        TrayIconEnabled = options.TrayIconEnabled;
        StartMinimizedToTray = options.StartMinimizedToTray;
        MinimizeToTray = options.MinimizeToTray;
        MinimizeToTrayOnClose = options.MinimizeToTrayOnClose;

        ComboBoxItem? theme = AppThemes.FirstOrDefault(a => a.Content != null && (string)a.Content == options.Theme) ?? null;
        CurrentAppTheme = theme ?? AppThemes[0];

        if (!string.IsNullOrEmpty(options.AccentColor) && options.AccentColor != Application.Current?.PlatformSettings?.GetColorValues().AccentColor1.ToString()){
            UseCustomAccent = true;
        }

        History = options.History;

        HistoryAutoRefreshModeHint = HistoryAutoRefreshMode switch{
            HistoryRefreshMode.DefaultAll =>
                "Refreshes the full history using the default method and includes all entries",
            HistoryRefreshMode.DefaultActive =>
                "Refreshes the history using the default method and includes only active entries",
            HistoryRefreshMode.FastNewReleases =>
                "Uses the faster refresh method, similar to the custom calendar, focusing on newly released items",
            _ => ""
        };
        
        settingsLoaded = true;
    }

    private void UpdateSettings(){
        if (!settingsLoaded){
            return;
        }

        var settings = CrunchyrollManager.Instance.CrunOptions;

        settings.DownloadFinishedPlaySound = DownloadFinishedPlaySound;
        
        settings.DownloadFinishedExecute = DownloadFinishedExecute;

        settings.DownloadMethodeNew = DownloadMethodeNew;
        settings.DownloadAllowEarlyStart = DownloadAllowEarlyStart;
        settings.DownloadOnlyWithAllSelectedDubSub = DownloadOnlyWithAllSelectedDubSub;

        settings.BackgroundImageBlurRadius = Math.Clamp((BackgroundImageBlurRadius ?? 0), 0, 40);
        settings.BackgroundImageOpacity = Math.Clamp((BackgroundImageOpacity ?? 0), 0, 1);

        settings.RetryAttempts = Math.Clamp((int)(RetryAttempts ?? 0), 1, 10);
        settings.RetryDelay = Math.Clamp((int)(RetryDelay ?? 0), 1, 30);

        settings.DownloadToTempFolder = DownloadToTempFolder;
        settings.HistoryCountMissing = HistoryCountMissing;
        settings.HistoryAddSpecials = HistoryAddSpecials;
        settings.HistoryIncludeCrArtists = HistoryIncludeCrArtists;
        settings.HistorySkipUnmonitored = HistorySkipUnmonitored;
        settings.HistoryCountSonarr = HistoryCountSonarr;
        settings.HistoryAutoRefreshIntervalMinutes =Math.Clamp((int)(HistoryAutoRefreshIntervalMinutes ?? 0), 0, 1000000000) ;
        settings.HistoryAutoRefreshMode = HistoryAutoRefreshMode;
        settings.DownloadSpeedLimit = Math.Clamp((int)(DownloadSpeed ?? 0), 0, 1000000000);
        settings.DownloadSpeedInBits = DownloadSpeedInBits;
        settings.SimultaneousDownloads = Math.Clamp((int)(SimultaneousDownloads ?? 0), 1, 10);
        settings.SimultaneousProcessingJobs = Math.Clamp((int)(SimultaneousProcessingJobs ?? 0), 1, 10);

        QueueManager.Instance.SetLimit(settings.SimultaneousProcessingJobs);

        settings.ProxyEnabled = ProxyEnabled;
        settings.ProxySocks = ProxySocks;
        settings.ProxyHost = ProxyHost;
        settings.ProxyPort = Math.Clamp((int)(ProxyPort ?? 0), 0, 65535);
        settings.ProxyUsername = ProxyUsername;
        settings.ProxyPassword = ProxyPassword;

        string historyLang = SelectedHistoryLang.Content + "";

        settings.HistoryLang = historyLang != "default" ? historyLang : CrunchyrollManager.Instance.DefaultLocale;

        settings.Theme = CurrentAppTheme?.Content + "";

        if (_faTheme.CustomAccentColor != (Application.Current?.PlatformSettings?.GetColorValues().AccentColor1)){
            settings.AccentColor = _faTheme.CustomAccentColor.ToString();
        } else{
            settings.AccentColor = string.Empty;
        }

        settings.History = History;

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
        
        settings.SonarrProperties = props;
        
        var propsFlareSolverr = new FlareSolverrProperties();

        propsFlareSolverr.UseSsl = FlareSolverrUseSsl;
        propsFlareSolverr.UseFlareSolverr = UseFlareSolverr;
        propsFlareSolverr.Host = FlareSolverrHost;

        if (int.TryParse(FlareSolverrPort, out var portNumberFlare)){
            propsFlareSolverr.Port = portNumberFlare;
        } else{
            propsFlareSolverr.Port = 8989;
        }

        settings.FlareSolverrProperties = propsFlareSolverr;
        
        settings.TrayIconEnabled = TrayIconEnabled;
        settings.StartMinimizedToTray = StartMinimizedToTray;
        settings.MinimizeToTray = MinimizeToTray;
        settings.MinimizeToTrayOnClose = MinimizeToTrayOnClose;

        settings.LogMode = LogMode;

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
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.DownloadDirPath ?? string.Empty,
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
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.DownloadTempDirPath ?? string.Empty,
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
            var folderPath = selectedFolder.Path.IsAbsoluteUri ? selectedFolder.Path.LocalPath : selectedFolder.Path.ToString();
            Console.WriteLine($"Selected folder: {folderPath}");
            pathSetter(folderPath);
            var finalPath = string.IsNullOrEmpty(pathGetter()) ? defaultPath : pathGetter();
            pathSetter(finalPath);
            CfgManager.WriteCrSettings();
        }
    }

    #region Background Image

    [RelayCommand]
    public void ClearBackgroundImagePath(){
        CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath = string.Empty;
        BackgroundImagePath = string.Empty;
        Helpers.SetBackgroundImage(string.Empty);
    }

    [RelayCommand]
    public async Task OpenImageFileDialogAsyncInternalBackgroundImage(){
        await OpenFileDialogAsyncInternal(
            title: "Select Image File",
            fileTypes: new List<FilePickerFileType>{
                new("Image Files"){
                    Patterns = new[]{ "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" }
                }
            },
            pathSetter: (path) => {
                CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath = path;
                BackgroundImagePath = path;
                Helpers.SetBackgroundImage(path, BackgroundImageOpacity, BackgroundImageBlurRadius);
            },
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.BackgroundImagePath ?? string.Empty,
            defaultPath: string.Empty
        );
    }

    #endregion


    #region Download Finished Sound

    [RelayCommand]
    public void ClearFinishedSoundPath(){
        CrunchyrollManager.Instance.CrunOptions.DownloadFinishedSoundPath = string.Empty;
        DownloadFinishedSoundPath = string.Empty;
    }

    [RelayCommand]
    public async Task OpenImageFileDialogAsyncInternalFinishedSound(){
        await OpenFileDialogAsyncInternal(
            title: "Select Audio File",
            fileTypes: new List<FilePickerFileType>{
                new("Audio Files"){
                    Patterns = new[]{ "*.mp3", "*.wav", "*.ogg", "*.flac", "*.aac" }
                }
            },
            pathSetter: (path) => {
                CrunchyrollManager.Instance.CrunOptions.DownloadFinishedSoundPath = path;
                DownloadFinishedSoundPath = path;
            },
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.DownloadFinishedSoundPath ?? string.Empty,
            defaultPath: string.Empty
        );
    }

    #endregion
    
    #region Download Finished Execute File

    [RelayCommand]
    public void ClearFinishedExectuePath(){
        CrunchyrollManager.Instance.CrunOptions.DownloadFinishedExecutePath = string.Empty;
        DownloadFinishedExecutePath = string.Empty;
    }

    [RelayCommand]
    public async Task OpenFileDialogAsyncInternalFinishedExecute(){
        await OpenFileDialogAsyncInternal(
            title: "Select File",
            fileTypes: new List<FilePickerFileType>{
                new("All Files"){
                    Patterns = new[]{ "*.*" }
                }
            },
            pathSetter: (path) => {
                CrunchyrollManager.Instance.CrunOptions.DownloadFinishedExecutePath = path;
                DownloadFinishedExecutePath = path;
            },
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.DownloadFinishedExecutePath ?? string.Empty,
            defaultPath: string.Empty
        );
    }
    
    #endregion

    private async Task OpenFileDialogAsyncInternal(
        string title,
        List<FilePickerFileType> fileTypes,
        Action<string> pathSetter,
        Func<string> pathGetter,
        string defaultPath){
        if (_storageProvider == null){
            Console.Error.WriteLine("StorageProvider must be set before using the dialog.");
            throw new InvalidOperationException("StorageProvider must be set before using the dialog.");
        }

        var result = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{
            Title = title,
            FileTypeFilter = fileTypes,
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
            Application.Current?.RequestedThemeVariant = ThemeVariant.Dark;
        } else{
            _faTheme.PreferSystemTheme = false;
            Application.Current?.RequestedThemeVariant = ThemeVariant.Light;
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
    partial void OnTrayIconEnabledChanged(bool value){
        ((App)Application.Current!).SetTrayIconVisible(value);
        UpdateSettings();
    }
    

    protected override void OnPropertyChanged(PropertyChangedEventArgs e){
        base.OnPropertyChanged(e);

        if (e.PropertyName is
            nameof(CustomAccentColor)
            or nameof(ListBoxColor)
            or nameof(CurrentAppTheme)
            or nameof(UseCustomAccent)
            or nameof(TrayIconEnabled)
            or nameof(LogMode)){
            return;
        }

        UpdateSettings();
        
        HistoryAutoRefreshModeHint = HistoryAutoRefreshMode switch{
            HistoryRefreshMode.DefaultAll =>
                "Refreshes the full history using the default method and includes all entries",
            HistoryRefreshMode.DefaultActive =>
                "Refreshes the history using the default method and includes only active entries",
            HistoryRefreshMode.FastNewReleases =>
                "Uses the faster refresh method, similar to the custom calendar, focusing on newly released items",
            _ => ""
        };

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
    public async Task CheckIp(){
        var result = await HttpClientReq.Instance.SendHttpRequest(HttpClientReq.CreateRequestMessage("https://icanhazip.com", HttpMethod.Get, false));
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