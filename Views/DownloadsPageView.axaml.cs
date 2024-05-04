using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CRD.Downloader;
using CRD.ViewModels;

namespace CRD.Views;

public partial class DownloadsPageView : UserControl{
    public DownloadsPageView(){
        InitializeComponent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e){
        base.OnDetachedFromVisualTree(e);
        if (DataContext is DownloadsPageViewModel vm){
            vm.Cleanup();
        }
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e){
        // Crunchy.Instance.TestMethode();
    }
}