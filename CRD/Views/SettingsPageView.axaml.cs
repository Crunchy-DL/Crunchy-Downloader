using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
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

    private void ListBox_PointerWheelChanged(object sender, Avalonia.Input.PointerWheelEventArgs e){
        var listBox = sender as ListBox;
        var scrollViewer = listBox?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

        if (scrollViewer != null){
            // Determine if the ListBox is at its bounds (top or bottom)
            bool atTop = scrollViewer.Offset.Y <= 0 && e.Delta.Y > 0;
            bool atBottom = scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height && e.Delta.Y < 0;

            if (atTop || atBottom){
                e.Handled = true; // Stop the event from propagating to the parent
            }
        }
    }
}