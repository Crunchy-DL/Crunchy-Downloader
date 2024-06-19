using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader;
using CRD.Utils.Updater;
using FluentAvalonia.Styling;
using Newtonsoft.Json;

namespace CRD.ViewModels;

public partial class MainWindowViewModel : ViewModelBase{
    private readonly FluentAvaloniaTheme _faTheme;

    [ObservableProperty]
    private bool _updateAvailable = true;
    
    public MainWindowViewModel(){
        _faTheme = App.Current.Styles[0] as FluentAvaloniaTheme;

        Init();

        CleanUpOldUpdater();

    }

    private void CleanUpOldUpdater() {
        string backupFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Updater.exe.bak");

        if (File.Exists(backupFilePath)) {
            try {
                File.Delete(backupFilePath);
                Console.WriteLine($"Deleted old updater file: {backupFilePath}");
            } catch (Exception ex) {
                Console.Error.WriteLine($"Failed to delete old updater file: {ex.Message}");
            }
        } else {
            Console.WriteLine("No old updater file found to delete.");
        }
    }


    public async void Init(){
        UpdateAvailable = await Updater.Instance.CheckForUpdatesAsync();

        await Crunchyroll.Instance.Init();

        if (Crunchyroll.Instance.CrunOptions.AccentColor != null){
            _faTheme.CustomAccentColor = Color.Parse(Crunchyroll.Instance.CrunOptions.AccentColor);
        }

        if (Crunchyroll.Instance.CrunOptions.Theme == "System"){
            _faTheme.PreferSystemTheme = true;
        } else if (Crunchyroll.Instance.CrunOptions.Theme == "Dark"){
            _faTheme.PreferSystemTheme = false;
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        } else{
            _faTheme.PreferSystemTheme = false;
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        }
    }
}