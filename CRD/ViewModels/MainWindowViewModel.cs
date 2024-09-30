using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader;

namespace CRD.ViewModels;

public partial class MainWindowViewModel : ViewModelBase{

    [ObservableProperty]
    public ProgramManager _programManager;
    
    public MainWindowViewModel(ProgramManager manager){
        ProgramManager = manager;
    }
    
}