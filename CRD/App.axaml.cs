using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CRD.ViewModels;
using MainWindow = CRD.Views.MainWindow;
using System.Linq;
using CRD.Downloader;

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
                
                desktop.MainWindow.Opened += (_, _) => { manager.SetBackgroundImage(); };
            }

            

        }

        base.OnFrameworkInitializationCompleted();
    }

    
}