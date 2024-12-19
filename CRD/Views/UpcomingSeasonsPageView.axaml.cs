using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CRD.Utils.Sonarr;
using CRD.Utils.Structs;
using CRD.ViewModels;

namespace CRD.Views;

public partial class UpcomingPageView : UserControl{
    public UpcomingPageView(){
        InitializeComponent();
    }

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e){
        if (DataContext is UpcomingPageViewModel viewModel && sender is ListBox listBox){
            viewModel.SelectionChangedOfSeries((AnilistSeries?)listBox.SelectedItem);
        }
    }
    
    private void ScrollViewer_PointerWheelChanged(object sender, Avalonia.Input.PointerWheelEventArgs e){
        if (sender is ScrollViewer scrollViewer){
            // Determine if the ListBox is at its bounds (top or bottom)
            bool atTop = scrollViewer.Offset.Y <= 0 && e.Delta.Y > 0;
            bool atBottom = scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height && e.Delta.Y < 0;

            if (atTop || atBottom){
                e.Handled = true; // Stop the event from propagating to the parent
            }
        }
    }
    
}