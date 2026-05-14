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
using CRD.Utils.Http;
using CRD.Utils.Notifications;
using CRD.Utils.Sonarr;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll;
using CRD.Utils.Structs.History;
using CRD.Views;
using FluentAvalonia.Styling;
using ReactiveUI;

namespace CRD.ViewModels.Utils;

public partial class GeneralSettingsViewModel : ViewModelBase{
    private readonly AudioPlayer notificationTestPlayer = new();

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
    private bool historyRemoveMissingEpisodes;

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
    private bool historyAutoRefreshAddToQueue;

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
    private bool persistQueue;

    [ObservableProperty]
    private double? downloadSpeed;
    
    [ObservableProperty]
    private bool downloadSpeedInBits;

    [ObservableProperty]
    private double? retryAttempts;

    [ObservableProperty]
    private double? retryDelay;

    [ObservableProperty]
    private double? playbackRateLimitRetryDelaySeconds;

    [ObservableProperty]
    private double? retryMaxDelaySeconds;
    
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
    private string mitmFlareSolverrHost = "localhost";

    [ObservableProperty]
    private string mitmFlareSolverrPort = "8080";

    [ObservableProperty]
    private bool mitmFlareSolverrUseSsl;

    [ObservableProperty]
    private bool useMitmFlareSolverr;

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
    private bool webhookEnabled;

    [ObservableProperty]
    private string webhookUrl = string.Empty;

    [ObservableProperty]
    private string webhookMethod = "POST";

    [ObservableProperty]
    private string webhookContentType = "application/json";

    [ObservableProperty]
    private string webhookHeadersText = string.Empty;

    [ObservableProperty]
    private string webhookBodyTemplate = string.Empty;

    [ObservableProperty]
    private bool webhookNotifyQueueFinished;

    [ObservableProperty]
    private bool webhookNotifyDownloadFinished;

    [ObservableProperty]
    private bool webhookNotifyDownloadFailed;

    [ObservableProperty]
    private bool webhookNotifyTrackedSeriesEpisodeReleased;

    [ObservableProperty]
    private bool webhookNotifyLoginExpired;

    [ObservableProperty]
    private bool webhookNotifyUpdateAvailable;

    [ObservableProperty]
    private string currentIp = "";

    [ObservableProperty]
    private bool isTestingFinishedSound;

    [ObservableProperty]
    private bool isTestingWebhook;

    private readonly FluentAvaloniaTheme faTheme;

    private bool settingsLoaded;

    private IStorageProvider? storageProvider;

    public GeneralSettingsViewModel(){
        storageProvider = ProgramManager.Instance.StorageProvider ?? throw new ArgumentNullException(nameof(ProgramManager.Instance.StorageProvider));

        var version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0];
        CurrentVersion = $"v{version}";

        faTheme = Application.Current?.Styles[0] as FluentAvaloniaTheme ??[];

        if (CrunchyrollManager.Instance.CrunOptions.AccentColor != null && !string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.AccentColor)){
            CustomAccentColor = Color.Parse(CrunchyrollManager.Instance.CrunOptions.AccentColor);
        } else{
            CustomAccentColor = Application.Current?.PlatformSettings?.GetColorValues().AccentColor1 ?? Colors.SlateBlue;
        }

        CrDownloadOptions options = CrunchyrollManager.Instance.CrunOptions;
        options.NormalizeNotificationSettings();

        BackgroundImageBlurRadius = options.BackgroundImageBlurRadius;
        BackgroundImageOpacity = options.BackgroundImageOpacity;
        BackgroundImagePath = options.BackgroundImagePath ?? string.Empty;

        var soundProvider = options.NotificationSettings?.GetOrCreateProvider(NotificationProviderType.Sound);
        DownloadFinishedSoundPath = soundProvider?.Path ?? string.Empty;
        DownloadFinishedPlaySound = soundProvider?.Enabled ?? false;
        
        var executeProvider = options.NotificationSettings?.GetOrCreateProvider(NotificationProviderType.Execute);
        DownloadFinishedExecutePath = executeProvider?.Path ?? string.Empty;
        DownloadFinishedExecute = executeProvider?.Enabled ?? false;

        var webhookProvider = options.NotificationSettings?.GetOrCreateProvider(NotificationProviderType.Webhook);
        WebhookEnabled = webhookProvider?.Enabled ?? false;
        WebhookUrl = webhookProvider?.Url ?? string.Empty;
        WebhookMethod = string.IsNullOrWhiteSpace(webhookProvider?.Method) ? "POST" : webhookProvider.Method;
        WebhookContentType = string.IsNullOrWhiteSpace(webhookProvider?.ContentType) ? "application/json" : webhookProvider.ContentType;
        WebhookHeadersText = SerializeHeaders(webhookProvider?.Headers);
        WebhookBodyTemplate = webhookProvider?.BodyTemplate ?? string.Empty;

        LoadProviderEvents(webhookProvider, NotificationProviderType.Webhook);

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
        
        var propsMitmFlareSolverr = options.FlareSolverrMitmProperties;

        if (propsMitmFlareSolverr != null){
            MitmFlareSolverrUseSsl = propsMitmFlareSolverr.UseSsl;
            UseMitmFlareSolverr = propsMitmFlareSolverr.UseMitmProxy;
            MitmFlareSolverrHost = propsMitmFlareSolverr.Host + "";
            MitmFlareSolverrPort = propsMitmFlareSolverr.Port + "";
        }

        ProxyEnabled = options.ProxyEnabled;
        ProxySocks = options.ProxySocks;
        ProxyHost = options.ProxyHost ?? "";
        ProxyUsername = options.ProxyUsername ?? "";
        ProxyPassword = options.ProxyPassword ?? "";
        ProxyPort = options.ProxyPort;
        HistoryCountMissing = options.HistoryCountMissing;
        HistoryIncludeCrArtists = options.HistoryIncludeCrArtists;
        HistoryRemoveMissingEpisodes = options.HistoryRemoveMissingEpisodes;
        HistoryAddSpecials = options.HistoryAddSpecials;
        HistorySkipUnmonitored = options.HistorySkipUnmonitored;
        HistoryCountSonarr = options.HistoryCountSonarr;
        HistoryAutoRefreshIntervalMinutes = options.HistoryAutoRefreshIntervalMinutes;
        HistoryAutoRefreshMode = options.HistoryAutoRefreshMode;
        HistoryAutoRefreshAddToQueue = options.HistoryAutoRefreshAddToQueue;
        HistoryAutoRefreshLastRunTime = ProgramManager.Instance.GetLastRefreshTime() == DateTime.MinValue ? "Never" : ProgramManager.Instance.GetLastRefreshTime().ToString("g", CultureInfo.CurrentCulture);
        DownloadSpeed = options.DownloadSpeedLimit;
        DownloadSpeedInBits = options.DownloadSpeedInBits;
        DownloadMethodeNew = options.DownloadMethodeNew;
        DownloadAllowEarlyStart = options.DownloadAllowEarlyStart;
        DownloadOnlyWithAllSelectedDubSub = options.DownloadOnlyWithAllSelectedDubSub;
        PersistQueue = options.PersistQueue;
        RetryAttempts = Math.Clamp((options.RetryAttempts), 1, 10);
        RetryDelay = Math.Clamp((options.RetryDelay), 1, 30);
        PlaybackRateLimitRetryDelaySeconds = Math.Clamp(options.PlaybackRateLimitRetryDelaySeconds, 1, 86400);
        RetryMaxDelaySeconds = Math.Clamp(options.RetryMaxDelaySeconds, 1, 86400);
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

        settings.NotificationSettings ??= new NotificationSettings();

        var soundProvider = settings.NotificationSettings.GetOrCreateProvider(NotificationProviderType.Sound);
        soundProvider.Enabled = DownloadFinishedPlaySound;
        soundProvider.Path = DownloadFinishedSoundPath;
        soundProvider.Events = [NotificationEventType.QueueFinished];

        var executeProvider = settings.NotificationSettings.GetOrCreateProvider(NotificationProviderType.Execute);
        executeProvider.Enabled = DownloadFinishedExecute;
        executeProvider.Path = DownloadFinishedExecutePath;
        executeProvider.Events = [NotificationEventType.QueueFinished];

        var webhookProvider = settings.NotificationSettings.GetOrCreateProvider(NotificationProviderType.Webhook);
        webhookProvider.Enabled = WebhookEnabled;
        webhookProvider.Url = WebhookUrl?.Trim() ?? string.Empty;
        webhookProvider.Method = string.IsNullOrWhiteSpace(WebhookMethod) ? "POST" : WebhookMethod.Trim().ToUpperInvariant();
        webhookProvider.ContentType = string.IsNullOrWhiteSpace(WebhookContentType) ? "application/json" : WebhookContentType.Trim();
        webhookProvider.Headers = ParseHeaders(WebhookHeadersText);
        webhookProvider.BodyTemplate = WebhookBodyTemplate ?? string.Empty;
        webhookProvider.Events = BuildEvents(
            WebhookNotifyQueueFinished,
            WebhookNotifyDownloadFinished,
            WebhookNotifyDownloadFailed,
            WebhookNotifyTrackedSeriesEpisodeReleased,
            WebhookNotifyLoginExpired,
            WebhookNotifyUpdateAvailable
        );

        settings.SyncLegacyNotificationFields();

        settings.DownloadMethodeNew = DownloadMethodeNew;
        settings.DownloadAllowEarlyStart = DownloadAllowEarlyStart;
        settings.DownloadOnlyWithAllSelectedDubSub = DownloadOnlyWithAllSelectedDubSub;
        settings.PersistQueue = PersistQueue;

        settings.BackgroundImageBlurRadius = Math.Clamp((BackgroundImageBlurRadius ?? 0), 0, 40);
        settings.BackgroundImageOpacity = Math.Clamp((BackgroundImageOpacity ?? 0), 0, 1);

        settings.RetryAttempts = Math.Clamp((int)(RetryAttempts ?? 0), 1, 10);
        settings.RetryDelay = Math.Clamp((int)(RetryDelay ?? 0), 1, 30);
        settings.PlaybackRateLimitRetryDelaySeconds = Math.Clamp((int)(PlaybackRateLimitRetryDelaySeconds ?? 0), 1, 86400);
        settings.RetryMaxDelaySeconds = Math.Clamp((int)(RetryMaxDelaySeconds ?? 0), 1, 86400);

        settings.DownloadToTempFolder = DownloadToTempFolder;
        settings.HistoryCountMissing = HistoryCountMissing;
        settings.HistoryAddSpecials = HistoryAddSpecials;
        settings.HistoryIncludeCrArtists = HistoryIncludeCrArtists;
        settings.HistoryRemoveMissingEpisodes = HistoryRemoveMissingEpisodes;
        settings.HistorySkipUnmonitored = HistorySkipUnmonitored;
        settings.HistoryCountSonarr = HistoryCountSonarr;
        settings.HistoryAutoRefreshIntervalMinutes =Math.Clamp((int)(HistoryAutoRefreshIntervalMinutes ?? 0), 0, 1000000000) ;
        settings.HistoryAutoRefreshMode = HistoryAutoRefreshMode;
        settings.HistoryAutoRefreshAddToQueue = HistoryAutoRefreshAddToQueue;
        settings.DownloadSpeedLimit = Math.Clamp((int)(DownloadSpeed ?? 0), 0, 1000000000);
        settings.DownloadSpeedInBits = DownloadSpeedInBits;
        settings.SimultaneousDownloads = Math.Clamp((int)(SimultaneousDownloads ?? 0), 1, 10);
        settings.SimultaneousProcessingJobs = Math.Clamp((int)(SimultaneousProcessingJobs ?? 0), 1, 10);

        QueueManager.Instance.SetProcessingLimit(settings.SimultaneousProcessingJobs);

        settings.ProxyEnabled = ProxyEnabled;
        settings.ProxySocks = ProxySocks;
        settings.ProxyHost = ProxyHost;
        settings.ProxyPort = Math.Clamp((int)(ProxyPort ?? 0), 0, 65535);
        settings.ProxyUsername = ProxyUsername;
        settings.ProxyPassword = ProxyPassword;

        string historyLang = SelectedHistoryLang.Content + "";

        settings.HistoryLang = historyLang != "default" ? historyLang : CrunchyrollManager.Instance.DefaultLocale;

        settings.Theme = CurrentAppTheme?.Content + "";

        if (faTheme.CustomAccentColor != (Application.Current?.PlatformSettings?.GetColorValues().AccentColor1)){
            settings.AccentColor = faTheme.CustomAccentColor.ToString();
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
        
        var propsMitmFlareSolverr = new MitmProxyProperties();

        propsMitmFlareSolverr.UseSsl = MitmFlareSolverrUseSsl;
        propsMitmFlareSolverr.UseMitmProxy = UseMitmFlareSolverr;
        propsMitmFlareSolverr.Host = MitmFlareSolverrHost;
        propsMitmFlareSolverr.UseSsl = MitmFlareSolverrUseSsl;
        
        if (int.TryParse(MitmFlareSolverrPort, out var portNumberMitmFlare)){
            propsMitmFlareSolverr.Port = portNumberMitmFlare;
        } else{
            propsMitmFlareSolverr.Port = 8080;
        }

        settings.FlareSolverrProperties = propsFlareSolverr;
        settings.FlareSolverrMitmProperties = propsMitmFlareSolverr;
        
        settings.TrayIconEnabled = TrayIconEnabled;
        settings.StartMinimizedToTray = StartMinimizedToTray;
        settings.MinimizeToTray = MinimizeToTray;
        settings.MinimizeToTrayOnClose = MinimizeToTrayOnClose;

        settings.LogMode = LogMode;

        CfgManager.WriteCrSettings();

        if (!PersistQueue){
            QueueManager.Instance.SaveQueueSnapshot();
        }
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
        if (storageProvider == null){
            Console.Error.WriteLine("StorageProvider must be set before using the dialog.");
            throw new InvalidOperationException("StorageProvider must be set before using the dialog.");
        }

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions{
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
        var settings = CrunchyrollManager.Instance.CrunOptions;
        settings.NotificationSettings ??= new NotificationSettings();
        settings.NotificationSettings.GetOrCreateProvider(NotificationProviderType.Sound).Path = string.Empty;
        settings.SyncLegacyNotificationFields();
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
                var validationResult = AudioPlayer.ValidateSoundFile(path);
                if (!validationResult.IsValid){
                    MessageBus.Current.SendMessage(new ToastMessage(validationResult.ErrorMessage, ToastType.Error, 5));
                    return;
                }

                var settings = CrunchyrollManager.Instance.CrunOptions;
                settings.NotificationSettings ??= new NotificationSettings();
                settings.NotificationSettings.GetOrCreateProvider(NotificationProviderType.Sound).Path = path;
                settings.SyncLegacyNotificationFields();
                DownloadFinishedSoundPath = path;
                MessageBus.Current.SendMessage(new ToastMessage("Notification sound updated", ToastType.Information, 2));
            },
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.NotificationSettings?.GetOrCreateProvider(NotificationProviderType.Sound).Path ?? string.Empty,
            defaultPath: string.Empty
        );
    }

    [RelayCommand]
    public async Task TestFinishedSoundAsync(){
        if (IsTestingFinishedSound){
            return;
        }

        var path = CrunchyrollManager.Instance.CrunOptions.NotificationSettings?.GetOrCreateProvider(NotificationProviderType.Sound).Path ?? string.Empty;
        IsTestingFinishedSound = true;

        try{
            var result = await notificationTestPlayer.ValidatePlaybackAsync(path);

            if (result.IsSuccess){
                MessageBus.Current.SendMessage(new ToastMessage("Notification sound test succeeded", ToastType.Information, 2));
                return;
            }

            MessageBus.Current.SendMessage(new ToastMessage($"Notification sound test failed: {result.ErrorMessage}", ToastType.Error, 5));
        } finally{
            IsTestingFinishedSound = false;
        }
    }

    [RelayCommand]
    public async Task StopFinishedSoundAsync(){
        await notificationTestPlayer.StopAsync();
        IsTestingFinishedSound = false;
    }

    [RelayCommand]
    public async Task TestWebhookAsync(){
        if (IsTestingWebhook){
            return;
        }

        var selectedEvents = BuildEvents(
            WebhookNotifyQueueFinished,
            WebhookNotifyDownloadFinished,
            WebhookNotifyDownloadFailed,
            WebhookNotifyTrackedSeriesEpisodeReleased,
            WebhookNotifyLoginExpired,
            WebhookNotifyUpdateAvailable
        );

        if (!WebhookEnabled){
            MessageBus.Current.SendMessage(new ToastMessage("Enable the webhook first", ToastType.Error, 4));
            return;
        }

        if (string.IsNullOrWhiteSpace(WebhookUrl)){
            MessageBus.Current.SendMessage(new ToastMessage("Set a webhook URL first", ToastType.Error, 4));
            return;
        }

        if (selectedEvents.Count == 0){
            MessageBus.Current.SendMessage(new ToastMessage("Select at least one webhook event to test", ToastType.Error, 4));
            return;
        }

        IsTestingWebhook = true;

        try{
            var settings = new NotificationSettings{
                Providers = [
                    new NotificationProviderConfig{
                        Type = NotificationProviderType.Webhook,
                        Enabled = true,
                        Url = WebhookUrl.Trim(),
                        Method = string.IsNullOrWhiteSpace(WebhookMethod) ? "POST" : WebhookMethod.Trim().ToUpperInvariant(),
                        ContentType = string.IsNullOrWhiteSpace(WebhookContentType) ? "application/json" : WebhookContentType.Trim(),
                        Headers = ParseHeaders(WebhookHeadersText),
                        BodyTemplate = WebhookBodyTemplate ?? string.Empty,
                        Events = selectedEvents
                    }
                ]
            };

            var sentCount = 0;

            foreach (var notificationEvent in BuildTestWebhookEvents(selectedEvents)){
                if (await NotificationDispatcher.Instance.PublishWithResultAsync(settings, notificationEvent)){
                    sentCount++;
                }
            }

            if (sentCount == selectedEvents.Count){
                MessageBus.Current.SendMessage(new ToastMessage($"Sent {sentCount} test webhook event(s)", ToastType.Information, 3));
            } else if (sentCount > 0){
                MessageBus.Current.SendMessage(new ToastMessage($"Sent {sentCount} of {selectedEvents.Count} test webhook event(s)", ToastType.Error, 5));
            } else{
                MessageBus.Current.SendMessage(new ToastMessage("Webhook test failed for all selected events", ToastType.Error, 5));
            }
        } finally{
            IsTestingWebhook = false;
        }
    }

    #endregion
    
    #region Download Finished Execute File

    [RelayCommand]
    public void ClearFinishedExectuePath(){
        var settings = CrunchyrollManager.Instance.CrunOptions;
        settings.NotificationSettings ??= new NotificationSettings();
        settings.NotificationSettings.GetOrCreateProvider(NotificationProviderType.Execute).Path = string.Empty;
        settings.SyncLegacyNotificationFields();
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
                var settings = CrunchyrollManager.Instance.CrunOptions;
                settings.NotificationSettings ??= new NotificationSettings();
                settings.NotificationSettings.GetOrCreateProvider(NotificationProviderType.Execute).Path = path;
                settings.SyncLegacyNotificationFields();
                DownloadFinishedExecutePath = path;
            },
            pathGetter: () => CrunchyrollManager.Instance.CrunOptions.NotificationSettings?.GetOrCreateProvider(NotificationProviderType.Execute).Path ?? string.Empty,
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
        if (storageProvider == null){
            Console.Error.WriteLine("StorageProvider must be set before using the dialog.");
            throw new InvalidOperationException("StorageProvider must be set before using the dialog.");
        }

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{
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

    private void LoadProviderEvents(NotificationProviderConfig? provider, NotificationProviderType type){
        var events = provider?.Events ?? [];

        switch (type){
            case NotificationProviderType.Webhook:
                WebhookNotifyQueueFinished = events.Contains(NotificationEventType.QueueFinished);
                WebhookNotifyDownloadFinished = events.Contains(NotificationEventType.DownloadFinished);
                WebhookNotifyDownloadFailed = events.Contains(NotificationEventType.DownloadFailed);
                WebhookNotifyTrackedSeriesEpisodeReleased = events.Contains(NotificationEventType.TrackedSeriesEpisodeReleased);
                WebhookNotifyLoginExpired = events.Contains(NotificationEventType.LoginExpired);
                WebhookNotifyUpdateAvailable = events.Contains(NotificationEventType.UpdateAvailable);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private static List<NotificationEventType> BuildEvents(
        bool queueFinished,
        bool downloadFinished,
        bool downloadFailed,
        bool trackedSeriesEpisodeReleased,
        bool loginExpired,
        bool updateAvailable){
        var events = new List<NotificationEventType>();

        if (queueFinished){
            events.Add(NotificationEventType.QueueFinished);
        }

        if (downloadFinished){
            events.Add(NotificationEventType.DownloadFinished);
        }

        if (downloadFailed){
            events.Add(NotificationEventType.DownloadFailed);
        }

        if (trackedSeriesEpisodeReleased){
            events.Add(NotificationEventType.TrackedSeriesEpisodeReleased);
        }

        if (loginExpired){
            events.Add(NotificationEventType.LoginExpired);
        }

        if (updateAvailable){
            events.Add(NotificationEventType.UpdateAvailable);
        }

        return events;
    }

    private static IEnumerable<NotificationEvent> BuildTestWebhookEvents(IEnumerable<NotificationEventType> selectedEvents){
        foreach (var eventType in selectedEvents.Distinct()){
            yield return BuildTestWebhookEvent(eventType);
        }
    }

    private static NotificationEvent BuildTestWebhookEvent(NotificationEventType eventType){
        return eventType switch{
            NotificationEventType.QueueFinished => new NotificationEvent{
                Type = NotificationEventType.QueueFinished,
                Title = "Downloads finished",
                Message = "All queued downloads have finished processing.",
                Metadata = []
            },
            NotificationEventType.DownloadFinished => new NotificationEvent{
                Type = NotificationEventType.DownloadFinished,
                Title = "Download finished",
                Message = "Finished processing Example Series.",
                Metadata = BuildTestDownloadMetadata()
            },
            NotificationEventType.DownloadFailed => new NotificationEvent{
                Type = NotificationEventType.DownloadFailed,
                Title = "Download failed",
                Message = "Failed to download Example Series: Example failure message",
                Metadata = BuildTestDownloadMetadata("Example failure message")
            },
            NotificationEventType.TrackedSeriesEpisodeReleased => new NotificationEvent{
                Type = NotificationEventType.TrackedSeriesEpisodeReleased,
                Title = "Tracked series episode released",
                Message = "A tracked episode is available for Example Series: Episode Title.",
                Metadata = new Dictionary<string, string>{
                    ["seriesTitle"] = "Example Series",
                    ["seriesId"] = "G6ABC1234",
                    ["seasonId"] = "G6SEASON01",
                    ["episodeTitle"] = "Episode Title",
                    ["episodeId"] = "G6EP0001",
                    ["episodeNumber"] = "1",
                    ["seasonNumber"] = "1",
                    ["releaseDate"] = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("O"),
                    ["premiumAvailableDate"] = DateTimeOffset.UtcNow.ToString("O"),
                    ["episodeUrl"] = "https://www.crunchyroll.com/en-US/watch/G6EP0001/episode-title",
                    ["imageUrl"] = "https://static.crunchyroll.com/example-thumbnail.jpg",
                    ["description"] = "Example tracked-release description.",
                    ["durationMs"] = "1440000",
                    ["availableDubs"] = "en-US, ja-JP",
                    ["availableSubs"] = "en-US, de-DE"
                }
            },
            NotificationEventType.LoginExpired => new NotificationEvent{
                Type = NotificationEventType.LoginExpired,
                Title = "Crunchyroll login expired",
                Message = "The saved Crunchyroll session could not be refreshed. Please log in again.",
                Metadata = new Dictionary<string, string>{
                    ["username"] = "example-user",
                    ["endpoint"] = "/auth/v1/token"
                }
            },
            NotificationEventType.UpdateAvailable => new NotificationEvent{
                Type = NotificationEventType.UpdateAvailable,
                Title = "Update available",
                Message = "Version v9.9.9 is available. Current version: v1.0.0.",
                Metadata = new Dictionary<string, string>{
                    ["currentVersion"] = "v1.0.0",
                    ["latestVersion"] = "v9.9.9",
                    ["platform"] = "win-x64",
                    ["downloadUrl"] = "https://github.com/Crunchy-DL/Crunchy-Downloader/releases/latest"
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null)
        };
    }

    private static Dictionary<string, string> BuildTestDownloadMetadata(string? error = null){
        var metadata = new Dictionary<string, string>{
            ["seriesTitle"] = "Example Series",
            ["seasonTitle"] = "Season 1",
            ["episodeTitle"] = "Episode Title",
            ["episodeNumber"] = "1",
            ["episodeId"] = "G6EP0001",
            ["downloadPath"] = @"C:\Downloads\Example Series\Season 1",
            ["seasonNumber"] = "1",
            ["description"] = "Example download description.",
            ["imageUrl"] = "https://static.crunchyroll.com/example-thumbnail.jpg",
            ["imageUrlLarge"] = "https://static.crunchyroll.com/example-poster.jpg",
            ["downloadSubs"] = "en-US, de-DE",
            ["downloadDubs"] = "ja-JP",
            ["hardsub"] = string.Empty,
            ["seriesId"] = "G6ABC1234",
            ["seasonId"] = "G6SEASON01",
            ["episodeUrl"] = "https://www.crunchyroll.com/watch/G6EP0001"
        };

        if (!string.IsNullOrWhiteSpace(error)){
            metadata["error"] = error;
        }

        return metadata;
    }

    private static string SerializeHeaders(IReadOnlyDictionary<string, string>? headers){
        if (headers == null || headers.Count == 0){
            return string.Empty;
        }

        return string.Join(Environment.NewLine, headers.Select(pair => $"{pair.Key}: {pair.Value}"));
    }

    private static Dictionary<string, string> ParseHeaders(string? headerText){
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(headerText)){
            return headers;
        }

        foreach (var rawLine in headerText.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)){
            var separatorIndex = rawLine.IndexOf(':');
            if (separatorIndex <= 0){
                continue;
            }

            var key = rawLine[..separatorIndex].Trim();
            var value = rawLine[(separatorIndex + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(key)){
                headers[key] = value;
            }
        }

        return headers;
    }


    partial void OnCurrentAppThemeChanged(ComboBoxItem? value){
        if (value?.Content?.ToString() == "System"){
            faTheme.PreferSystemTheme = true;
        } else if (value?.Content?.ToString() == "Dark"){
            faTheme.PreferSystemTheme = false;
            Application.Current?.RequestedThemeVariant = ThemeVariant.Dark;
        } else{
            faTheme.PreferSystemTheme = false;
            Application.Current?.RequestedThemeVariant = ThemeVariant.Light;
        }

        UpdateSettings();
    }

    partial void OnUseCustomAccentChanged(bool value){
        if (value){
            if (faTheme.TryGetResource("SystemAccentColor", null, out var curColor)){
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
        faTheme.CustomAccentColor = color;
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
            or nameof(LogMode)
            or nameof(PersistQueue)){
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

    partial void OnPersistQueueChanged(bool value){
        UpdateSettings();
        QueueManager.Instance.SaveQueueSnapshot();
    }
}
