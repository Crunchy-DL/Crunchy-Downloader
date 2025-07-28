using Avalonia.Controls;
using Avalonia.Interactivity;
using CRD.Utils.Sonarr;
using CRD.ViewModels;

namespace CRD.Views;

public partial class SettingsPageView : UserControl{
    public SettingsPageView(){
        InitializeComponent();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e){
        if (DataContext is SettingsPageViewModel viewModel){
            SonarrClient.Instance.RefreshSonarr();
        }
    }
    
}