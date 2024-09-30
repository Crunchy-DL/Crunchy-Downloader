using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CRD.ViewModels;
using MainWindow = CRD.Views.MainWindow;
using System.Linq;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Updater;

namespace CRD;

public partial class App : Application{
    public override void Initialize(){
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted(){
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){
            var isHeadless = Environment.GetCommandLineArgs().Contains("--headless");

            var manager = ProgramManager.Instance;
            
            if (!isHeadless){
                desktop.MainWindow = new MainWindow{
                    DataContext = new MainWindowViewModel(manager),
                };
            }

            

        }

        base.OnFrameworkInitializationCompleted();
    }

    
}