using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CRD.ViewModels;
using MainWindow = CRD.Views.MainWindow;

namespace CRD;

public partial class App : Application{
    public override void Initialize(){
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted(){
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){
            desktop.MainWindow = new MainWindow{
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}