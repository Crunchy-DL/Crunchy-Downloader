using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.Utils.Updater;
using CRD.ViewModels;
using CRD.Views;
using CRD.Views.Utils;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Newtonsoft.Json;
using ReactiveUI;
using ContentDialogUpdateViewModel = CRD.ViewModels.Utils.ContentDialogUpdateViewModel;

namespace CRD.Views;

public partial class MainWindow : AppWindow{
    private Stack<object> navigationStack = new Stack<object>();


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
                    navigationStack.Pop();
                    var viewModel = Activator.CreateInstance(message.ViewModelType);
                    if (viewModel is SeriesPageViewModel){
                        ((SeriesPageViewModel)viewModel).SetStorageProvider(StorageProvider);
                    }

                    navigationStack.Push(viewModel);
                    nv.Content = viewModel;
                } else if (!message.Back && message.ViewModelType != null){
                    var viewModel = Activator.CreateInstance(message.ViewModelType);
                    if (viewModel is SeriesPageViewModel){
                        ((SeriesPageViewModel)viewModel).SetStorageProvider(StorageProvider);
                    }

                    navigationStack.Push(viewModel);
                    nv.Content = viewModel;
                } else{
                    navigationStack.Pop();
                    var viewModel = navigationStack.Peek();
                    nv.Content = viewModel;
                }
            });

        MessageBus.Current.Listen<ToastMessage>()
            .Subscribe(message => ShowToast(message.Message, message.Type, message.Seconds));
    }

    public async void ShowError(string message){
        var dialog = new ContentDialog(){
            Title = "Error",
            Content = message,
            CloseButtonText = "Close"
        };

        _ = await dialog.ShowAsync();
    }


    public void ShowToast(string message, ToastType type, int durationInSeconds = 5){
        this.FindControl<ToastNotification>("Toast").Show(message, type, durationInSeconds);
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
                        if (navView.Content is HistoryPageViewModel){
                            ((HistoryPageViewModel)navView.Content).SetStorageProvider(StorageProvider);
                        }

                        navigationStack.Clear();
                        navigationStack.Push(navView.Content);
                        selectedNavVieItem = selectedItem;
                        break;
                    case "Account":
                        navView.Content = Activator.CreateInstance(typeof(AccountPageViewModel));
                        selectedNavVieItem = selectedItem;
                        break;
                    case "Settings":
                        var viewModel = (SettingsPageViewModel)Activator.CreateInstance(typeof(SettingsPageViewModel));
                        viewModel.SetStorageProvider(StorageProvider);
                        navView.Content = viewModel;
                        selectedNavVieItem = selectedItem;
                        break;
                    case "UpdateAvailable":
                        Updater.Instance.DownloadAndUpdateAsync();
                        ShowUpdateDialog();
                        break;
                    default:
                        // (sender as NavigationView).Content = Activator.CreateInstance(typeof(DownloadsPageViewModel));
                        break;
                }
            }
        }
    }

    public async void ShowUpdateDialog(){
        var dialog = new ContentDialog(){
            Title = "Updating",
            // CloseButtonText = "Close"
        };

        var viewModel = new ContentDialogUpdateViewModel(dialog);
        dialog.Content = new ContentDialogUpdateView(){
            DataContext = viewModel
        };

        _ = await dialog.ShowAsync();
    }

    private void OnOpened(object sender, EventArgs e){
        if (File.Exists(CfgManager.PathWindowSettings)){
            var settings = JsonConvert.DeserializeObject<WindowSettings>(File.ReadAllText(CfgManager.PathWindowSettings));
            if (settings != null){
                var screens = Screens.All;
                if (settings.ScreenIndex >= 0 && settings.ScreenIndex < screens.Count){
                    var screen = screens[settings.ScreenIndex];
                    var screenBounds = screen.Bounds;

                    // Restore the position first
                    Position = new PixelPoint(settings.PosX, settings.PosY + TitleBarHeightAdjustment);

                    // Restore the size
                    Width = settings.Width;
                    Height = settings.Height - TitleBarHeightAdjustment;

                    // Set restore size and position for non-maximized state
                    _restoreSize = new Size(settings.Width, settings.Height);
                    _restorePosition = new PixelPoint(settings.PosX, settings.PosY + TitleBarHeightAdjustment);

                    // Ensure the window is on the correct screen before maximizing
                    Position = new PixelPoint(settings.PosX, settings.PosY+ TitleBarHeightAdjustment);
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
            Width = this.WindowState == WindowState.Maximized ? _restoreSize.Width : Width,
            Height = this.WindowState == WindowState.Maximized ? _restoreSize.Height : Height,
            ScreenIndex = screenIndex,
            PosX = this.WindowState == WindowState.Maximized ? _restorePosition.X : Position.X,
            PosY = this.WindowState == WindowState.Maximized ? _restorePosition.Y : Position.Y,
            IsMaximized = this.WindowState == WindowState.Maximized
        };

        File.WriteAllText(CfgManager.PathWindowSettings, JsonConvert.SerializeObject(settings, Formatting.Indented));
    }

    private void OnWindowStateChanged(object sender, AvaloniaPropertyChangedEventArgs e){
        if (e.Property == Window.WindowStateProperty){
            if (WindowState == WindowState.Normal){
                // When the window is restored to normal, use the stored restore size and position
                Width = _restoreSize.Width;
                Height = _restoreSize.Height;
                Position = _restorePosition;
            }
        }
    }

    private void OnPositionChanged(object sender, PixelPointEventArgs e){
        if (WindowState == WindowState.Normal){
            _restorePosition = e.Point;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e){
        if (WindowState == WindowState.Normal){
            _restoreSize = e.NewSize;
        }
    }
}