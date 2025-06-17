using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using CRD.Downloader.Crunchyroll.ViewModels;
using CRD.Downloader.Crunchyroll.Views;
using CRD.ViewModels.Utils;
using CRD.Views.Utils;
using FluentAvalonia.UI.Controls;
using Image = Avalonia.Controls.Image;

namespace CRD.ViewModels;

public class SettingsPageViewModel : ViewModelBase{
    
    public ObservableCollection<TabViewItem> Tabs{ get; } = new();

    private TabViewItem CreateTab(string header, string iconPath, UserControl content, object viewModel){
        content.DataContext = viewModel;

        Bitmap? bitmap = null;
        try{
            // Load the image using AssetLoader.Open
            bitmap = new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri(iconPath)));
        } catch (Exception ex){
            Console.WriteLine($"Error loading image: {ex.Message}");
        }

        return new TabViewItem{
            Header = new StackPanel{
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children ={
                    new Image{ Source = bitmap, Width = 18, Height = 18 },
                    new TextBlock{ Text = header, FontSize = 16}
                }
            },
            IsClosable = false,
            Content = content
        };
    }

    public SettingsPageViewModel(){
        // Add initial tabs
        Tabs.Add(CreateTab("General Settings", "avares://CRD/Assets/app_icon.ico", new GeneralSettingsView(), new GeneralSettingsViewModel()));
        Tabs.Add(CreateTab("Crunchyroll Settings", "avares://CRD/Assets/crunchy_icon_round.png", new CrunchyrollSettingsView(), new CrunchyrollSettingsViewModel()));
        
    }

  
}