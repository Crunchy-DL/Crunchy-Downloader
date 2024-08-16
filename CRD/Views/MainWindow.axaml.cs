using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using CRD.Utils;
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

    public MainWindow(){
        AvaloniaXamlLoader.Load(this);
        InitializeComponent();

        Opened += OnOpened;
        Closing += OnClosing;

        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;


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
                Width = settings.Width;
                Height = settings.Height;

                var screens = Screens.All;
                if (settings.ScreenIndex >= 0 && settings.ScreenIndex < screens.Count){
                    var screen = screens[settings.ScreenIndex];
                    var screenBounds = screen.Bounds;

                    var topLeft = screenBounds.TopLeft;
                    var bottomRight = screenBounds.BottomRight;

                    if (settings.PosX >= topLeft.X && settings.PosX <= bottomRight.X - Width &&
                        settings.PosY >= topLeft.Y && settings.PosY <= bottomRight.Y - Height){
                        Position = new PixelPoint(settings.PosX, settings.PosY);
                    } else{
                        Position = new PixelPoint(topLeft.X, topLeft.Y + 31);
                    }
                } else{
                    var primaryScreen = screens?[0].Bounds ?? new PixelRect(0, 0, 1000, 600); // Default size if no screens
                    Position = new PixelPoint(primaryScreen.TopLeft.X, primaryScreen.TopLeft.Y + 31);
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
            Width = Width,
            Height = Height,
            ScreenIndex = screenIndex,
            PosX = Position.X,
            PosY = Position.Y
        };

        File.WriteAllText(CfgManager.PathWindowSettings, JsonConvert.SerializeObject(settings, Formatting.Indented));
    }
}