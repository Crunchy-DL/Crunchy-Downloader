using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CRD.Downloader;
using CRD.Utils.Sonarr;
using CRD.ViewModels;

namespace CRD.Views;

public partial class SettingsPageView : UserControl{
    public SettingsPageView(){
        InitializeComponent();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e){
        if (DataContext is SettingsPageViewModel viewModel){
            Crunchyroll.Instance.RefreshSonarr();
        }
    }
}