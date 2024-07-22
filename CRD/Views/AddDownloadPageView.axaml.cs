using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CRD.ViewModels;

namespace CRD.Views;

public partial class AddDownloadPageView : UserControl{
    public AddDownloadPageView(){
        InitializeComponent();
    }

    private void Popup_Closed(object? sender, EventArgs e){
        if (DataContext is AddDownloadPageViewModel viewModel){
            viewModel.SearchPopupVisible = false;
        }
    }
}