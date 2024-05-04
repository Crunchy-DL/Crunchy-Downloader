using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Disposables;
using Avalonia.Controls;
using CRD.Downloader;
using CRD.ViewModels;
using CRD.Views.Utils;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using FluentAvalonia.UI.Windowing;
using ReactiveUI;

namespace CRD.Views;

public partial class MainWindow : AppWindow{
    private Stack<object> navigationStack = new Stack<object>();

    public MainWindow(){
        InitializeComponent();
        
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;


        //select first element as default
        var nv = this.FindControl<NavigationView>("NavView");
        nv.SelectedItem = nv.MenuItems.ElementAt(0);

        MessageBus.Current.Listen<NavigationMessage>()
            .Subscribe(message => {
                if (message.Refresh){
                    navigationStack.Pop();
                    var viewModel = Activator.CreateInstance(message.ViewModelType);
                    navigationStack.Push(viewModel);
                    nv.Content = viewModel;
                } else if (!message.Back && message.ViewModelType != null){
                    var viewModel = Activator.CreateInstance(message.ViewModelType);
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

    public static void ShowError(string message){
        var window = new ErrorWindow();
        window.SetErrorMessage(message);
        window.Show(); // 'this' is a reference to the parent window, if applicable
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
                        (sender as NavigationView).Content = Activator.CreateInstance(typeof(DownloadsPageViewModel));
                        break;
                    case "AddDownload":
                        (sender as NavigationView).Content = Activator.CreateInstance(typeof(AddDownloadPageViewModel));
                        break;
                    case "Calendar":
                        (sender as NavigationView).Content = Activator.CreateInstance(typeof(CalendarPageViewModel));
                        break;
                    case "History":
                        (sender as NavigationView).Content = Activator.CreateInstance(typeof(HistoryPageViewModel));
                        navigationStack.Clear();
                        navigationStack.Push((sender as NavigationView).Content);
                        break;
                    case "Account":
                        (sender as NavigationView).Content = Activator.CreateInstance(typeof(AccountPageViewModel));
                        break;
                    case "Settings":
                        (sender as NavigationView).Content = Activator.CreateInstance(typeof(SettingsPageViewModel));
                        break;
                    default:
                        (sender as NavigationView).Content = Activator.CreateInstance(typeof(DownloadsPageViewModel));
                        break;
                }
            }
        }
    }
}

public class ToastMessage(string message, ToastType type, int i){
    public string? Message{ get; set; } = message;
    public int Seconds{ get; set; } = i;
    public ToastType Type{ get; set; } = type;
}

public class NavigationMessage{
    public Type? ViewModelType{ get; }
    public bool Back{ get; }
    public bool Refresh{ get; }

    public NavigationMessage(Type? viewModelType, bool back, bool refresh){
        ViewModelType = viewModelType;
        Back = back;
        Refresh = refresh;
    }
}