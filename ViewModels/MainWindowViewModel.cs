using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CRD.Downloader;
using FluentAvalonia.Styling;

namespace CRD.ViewModels;

public partial class MainWindowViewModel : ViewModelBase{
    private readonly FluentAvaloniaTheme _faTheme;

    public MainWindowViewModel(){
        
        _faTheme = App.Current.Styles[0] as FluentAvaloniaTheme;
        
        Init();
        
    }

    public async void Init(){
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