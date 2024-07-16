using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CRD.ViewModels;

public class ViewModelBase : ObservableObject{
    public event PropertyChangedEventHandler PropertyChanged;
    
    protected void RaisePropertyChanged(string propName){
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}