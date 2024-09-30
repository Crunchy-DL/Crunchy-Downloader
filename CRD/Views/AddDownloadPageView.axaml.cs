using System;
using System.Runtime;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CRD.ViewModels;

namespace CRD.Views;

public partial class AddDownloadPageView : UserControl{
    public AddDownloadPageView(){
        InitializeComponent();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e){
        if (DataContext is AddDownloadPageViewModel viewModel){
            viewModel.Dispose();
            DataContext = null;
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }
    }

    private void Popup_Closed(object? sender, EventArgs e){
        if (DataContext is AddDownloadPageViewModel viewModel){
            viewModel.SearchPopupVisible = false;
        }
    }
}