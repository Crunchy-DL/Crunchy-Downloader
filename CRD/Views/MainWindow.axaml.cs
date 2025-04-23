using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.ViewModels;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Newtonsoft.Json;
using ReactiveUI;
using UpdateViewModel = CRD.ViewModels.UpdateViewModel;

namespace CRD.Views;

public partial class MainWindow : AppWindow{
    private Stack<object> navigationStack = new Stack<object>();

    private static HashSet<string> activeErrors = new HashSet<string>();

    #region Singelton

    private static MainWindow? _instance;
    private static readonly object Padlock = new();

    public static MainWindow Instance{
        get{
            if (_instance == null){
                lock (Padlock){
                    if (_instance == null){
                        _instance = new MainWindow();
                    }
                }
            }

            return _instance;
        }
    }

    #endregion

    private object selectedNavVieItem;

    private const int TitleBarHeightAdjustment = 31;

    private PixelPoint _restorePosition;
    private Size _restoreSize;

    public MainWindow(){
        ProgramManager.Instance.StorageProvider = StorageProvider;
        AvaloniaXamlLoader.Load(this);
        InitializeComponent();

        ExtendClientAreaTitleBarHeightHint = TitleBarHeightAdjustment;
        TitleBar.Height = TitleBarHeightAdjustment;
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

        Opened += OnOpened;
        Closing += OnClosing;

        PropertyChanged += OnWindowStateChanged;

        PositionChanged += OnPositionChanged;
        SizeChanged += OnSizeChanged;


        //select first element as default
        var nv = this.FindControl<NavigationView>("NavView");
        nv.SelectedItem = nv.MenuItems.ElementAt(0);
        selectedNavVieItem = nv.SelectedItem;

        MessageBus.Current.Listen<NavigationMessage>()
            .Subscribe(message => {
                if (message.Refresh){
                    if (navigationStack.Count > 0){
                        navigationStack.Pop();
                    }

                    try{
                        var viewModel = Activator.CreateInstance(message.ViewModelType);
                        navigationStack.Push(viewModel);
                        nv.Content = viewModel;
                    } catch (Exception ex){
                        Console.Error.WriteLine($"Failed to create or push viewModel: {ex.Message}");
                    }
                } else if (message is{ Back: false, ViewModelType: not null }){
                    try{
                        var viewModel = Activator.CreateInstance(message.ViewModelType);
                        navigationStack.Push(viewModel);
                        nv.Content = viewModel;
                    } catch (Exception ex){
                        Console.Error.WriteLine($"Failed to create or push viewModel: {ex.Message}");
                    }
                } else{
                    if (navigationStack.Count > 0){
                        navigationStack.Pop();
                    }

                    if (navigationStack.Count > 0){
                        var viewModel = navigationStack.Peek();
                        if (viewModel is HistoryPageViewModel historyView){
                            historyView.ApplyFilter();
                        }

                        nv.Content = viewModel;
                    } else{
                        Console.Error.WriteLine("Navigation stack is empty. Cannot peek.");
                    }
                }
            });

        MessageBus.Current.Listen<ToastMessage>()
            .Subscribe(message => ShowToast(message.Message ?? string.Empty, message.Type, message.Seconds));
    }

    public async void ShowError(string message, bool githubWikiButton = false){
        if (activeErrors.Contains(message))
            return;

        activeErrors.Add(message);

        var dialog = new ContentDialog(){
            Title = "Error",
            Content = message,
            CloseButtonText = "Close"
        };

        if (githubWikiButton){
            dialog.PrimaryButtonText = "Github Wiki";
        }

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary){
            Helpers.OpenUrl($"https://github.com/Crunchy-DL/Crunchy-Downloader/wiki");
        }

        activeErrors.Remove(message);
    }


    public void ShowToast(string message, ToastType type, int durationInSeconds = 5){
        var toastControl = this.FindControl<ToastNotification>("Toast");
        toastControl?.Show(message, type, durationInSeconds);
    }


    private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e){
        if (sender is NavigationView navView){
            var selectedItem = navView.SelectedItem as NavigationViewItem;
            if (selectedItem != null){
                switch (selectedItem.Tag){
                    case "DownloadQueue":
                        navView.Content = Activator.CreateInstance(typeof(DownloadsPageViewModel));
                        selectedNavVieItem = selectedItem;
                        break;
                    case "AddDownload":
                        navView.Content = Activator.CreateInstance(typeof(AddDownloadPageViewModel));
                        selectedNavVieItem = selectedItem;
                        break;
                    case "Calendar":
                        navView.Content = Activator.CreateInstance(typeof(CalendarPageViewModel));
                        selectedNavVieItem = selectedItem;
                        break;
                    case "History":
                        navView.Content = Activator.CreateInstance(typeof(HistoryPageViewModel));
                        navigationStack.Clear();
                        navigationStack.Push(navView.Content);
                        selectedNavVieItem = selectedItem;
                        break;
                    case "Seasons":
                        navView.Content = Activator.CreateInstance(typeof(UpcomingPageViewModel));
                        selectedNavVieItem = selectedItem;
                        break;
                    case "Account":
                        navView.Content = Activator.CreateInstance(typeof(AccountPageViewModel));
                        selectedNavVieItem = selectedItem;
                        break;
                    case "Settings":
                        var viewModel = (SettingsPageViewModel)Activator.CreateInstance(typeof(SettingsPageViewModel));
                        navView.Content = viewModel;
                        selectedNavVieItem = selectedItem;
                        break;
                    case "Update":
                        navView.Content = Activator.CreateInstance(typeof(UpdateViewModel));
                        selectedNavVieItem = selectedItem;
                        break;
                    default:
                        // (sender as NavigationView).Content = Activator.CreateInstance(typeof(DownloadsPageViewModel));
                        break;
                }
            }
        }
    }

    private void OnOpened(object sender, EventArgs e){
        if (File.Exists(CfgManager.PathWindowSettings)){
            var settings = JsonConvert.DeserializeObject<WindowSettings>(File.ReadAllText(CfgManager.PathWindowSettings));
            if (settings != null){
                var screens = Screens.All;
                if (settings.ScreenIndex >= 0 && settings.ScreenIndex < screens.Count){
                    var screen = screens[settings.ScreenIndex];

                    // Restore the position first
                    Position = new PixelPoint(settings.PosX, settings.PosY);

                    // Restore the size
                    Width = settings.Width;
                    Height = settings.Height - TitleBarHeightAdjustment;

                    // Set restore size and position for non-maximized state
                    _restoreSize = new Size(settings.Width, settings.Height);
                    _restorePosition = new PixelPoint(settings.PosX, settings.PosY);

                    // Ensure the window is on the correct screen before maximizing
                    Position = new PixelPoint(settings.PosX, settings.PosY);
                }

                if (settings.IsMaximized){
                    // Maximize the window after setting its position on the correct screen
                    WindowState = WindowState.Maximized;
                }
            }
        }
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e){
        var screens = Screens.All;
        int screenIndex = 0;

        for (int i = 0; i < screens.Count; i++){
            if (screens[i].Bounds.Contains(Position)){
                screenIndex = i;
                break;
            }
        }

        var settings = new WindowSettings{
            Width = WindowState == WindowState.Maximized ? _restoreSize.Width : Width,
            Height = WindowState == WindowState.Maximized ? _restoreSize.Height : Height,
            ScreenIndex = screenIndex,
            PosX = WindowState == WindowState.Maximized ? _restorePosition.X : Position.X,
            PosY = WindowState == WindowState.Maximized ? _restorePosition.Y : Position.Y,
            IsMaximized = WindowState == WindowState.Maximized
        };

        File.WriteAllText(CfgManager.PathWindowSettings, JsonConvert.SerializeObject(settings, Formatting.Indented));

        FileNameManager.DeleteEmptyFolders(!string.IsNullOrEmpty(CrunchyrollManager.Instance.CrunOptions.DownloadTempDirPath) ? CrunchyrollManager.Instance.CrunOptions.DownloadTempDirPath : CfgManager.PathTEMP_DIR);
    }

    private void OnWindowStateChanged(object sender, AvaloniaPropertyChangedEventArgs e){
        if (e.Property == WindowStateProperty){
            if (WindowState == WindowState.Normal){
                Width = _restoreSize.Width;
                Height = _restoreSize.Height;
                Position = _restorePosition;
            }
        }
    }

    private void OnPositionChanged(object sender, PixelPointEventArgs e){
        if (WindowState == WindowState.Normal){
            var screens = Screens.All;

            bool isWithinAnyScreen = screens.Any(screen =>
                e.Point.X >= screen.WorkingArea.X &&
                e.Point.X <= screen.WorkingArea.X + screen.WorkingArea.Width &&
                e.Point.Y >= screen.WorkingArea.Y &&
                e.Point.Y <= screen.WorkingArea.Y + screen.WorkingArea.Height);

            if (isWithinAnyScreen){
                _restorePosition = e.Point;
            }
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e){
        if (WindowState == WindowState.Normal){
            _restoreSize = e.NewSize;
        }
    }
}