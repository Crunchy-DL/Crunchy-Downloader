using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace CRD.Downloader.Crunchyroll.Views;

public partial class CrunchyrollSettingsView : UserControl{
    public CrunchyrollSettingsView(){
        InitializeComponent();
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