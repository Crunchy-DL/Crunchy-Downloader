using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Utils.Updater;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogUpdateViewModel : ViewModelBase{
    private readonly ContentDialog dialog;

    [ObservableProperty]
    private double _progress;


    private AccountPageViewModel accountPageViewModel;

    public ContentDialogUpdateViewModel(ContentDialog dialog){
        if (dialog is null){
            throw new ArgumentNullException(nameof(dialog));
        }

        this.dialog = dialog;
        dialog.Closed += DialogOnClosed;
        Updater.Instance.PropertyChanged += Progress_PropertyChanged;
    }

    private void Progress_PropertyChanged(object? sender, PropertyChangedEventArgs e){
        if (e.PropertyName == nameof(Updater.Instance.progress)){
            Progress = Updater.Instance.progress;
        }
    }


    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}