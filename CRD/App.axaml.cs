using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CRD.ViewModels;
using MainWindow = CRD.Views.MainWindow;
using Avalonia.Controls;
using Avalonia.Platform;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;

namespace CRD;

public class App : Application{
    
    private TrayIcon? trayIcon;
    private bool exitRequested;
    
    public override void Initialize(){
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted(){
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){
            var isHeadless = Environment.GetCommandLineArgs().Contains("--headless");

            var manager = ProgramManager.Instance;
            
            if (!isHeadless){
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                
                var mainWindow = new MainWindow{
                    DataContext = new MainWindowViewModel(manager),
                };
                
                mainWindow.Opened += (_, _) => { manager.SetBackgroundImage(); };
                desktop.Exit += (_, _) => { manager.StopBackgroundTasks(); };
                
                if (!CrunchyrollManager.Instance.CrunOptions.StartMinimizedToTray){
                    desktop.MainWindow = mainWindow;
                }

                SetupTrayIcon(desktop, mainWindow, manager);
                SetupMinimizeToTray(desktop,mainWindow,manager);
            }

            

        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, Window mainWindow, ProgramManager programManager){
        trayIcon = new TrayIcon{
            ToolTipText = "CRD",
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CRD/Assets/app_icon.ico"))),
            IsVisible = CrunchyrollManager.Instance.CrunOptions.TrayIconEnabled,
        };

        var menu = new NativeMenu();
        
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => {
            exitRequested = true;
            trayIcon?.Dispose();
            desktop.Shutdown();
        };
        menu.Items.Add(exitItem);

        trayIcon.Menu = menu;

        trayIcon.Clicked += (_, _) => ShowFromTray(desktop, mainWindow);
        
        TrayIcon.SetIcons(this, new TrayIcons{ trayIcon });
    }

    private void SetupMinimizeToTray(IClassicDesktopStyleApplicationLifetime desktop, Window window , ProgramManager programManager){
        window.Closing += (_, e) => {
            if (exitRequested)
                return;
            
            if (CrunchyrollManager.Instance.CrunOptions is{ MinimizeToTrayOnClose: true, TrayIconEnabled: true }){
                HideToTray(window);
                e.Cancel = true;
                return;
            }
            
            exitRequested = true;
            trayIcon?.Dispose();
            desktop.Shutdown();
        };
        
        window.GetObservable(Window.WindowStateProperty).Subscribe(state => {
            if (CrunchyrollManager.Instance.CrunOptions is{ TrayIconEnabled: true, MinimizeToTray: true } && state == WindowState.Minimized)
                HideToTray(window);
        });
    }

    private static void HideToTray(Window window){
        window.ShowInTaskbar = false;
        window.Hide();
    }

    private void ShowFromTray(IClassicDesktopStyleApplicationLifetime desktop, Window mainWindow){
        desktop.MainWindow ??= mainWindow;
        RestoreFromTray(mainWindow);
    }

    private static void RestoreFromTray(Window window){
        window.ShowInTaskbar = true;
        window.Show();

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Activate();
    }

    public void SetTrayIconVisible(bool enabled){
        trayIcon?.IsVisible = enabled;
        
        if (!enabled && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){
            if (desktop.MainWindow is{ } w)
                RestoreFromTray(w);
        }
    }
    
    
}