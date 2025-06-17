using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CRD.ViewModels;

namespace CRD.Views;

public partial class HistoryPageView : UserControl{
    public HistoryPageView(){
        InitializeComponent();
    }
    
    private void OnUnloaded(object? sender, RoutedEventArgs e){
        
        if (DataContext is HistoryPageViewModel viewModel){
            viewModel.LastScrollOffset = SeriesListBox.Scroll?.Offset ?? Vector.Zero;
        }
        
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e){
        if (DataContext is HistoryPageViewModel viewModel){
            if (SeriesListBox.Scroll != null) SeriesListBox.Scroll.Offset = viewModel.LastScrollOffset;
        }
    }
}