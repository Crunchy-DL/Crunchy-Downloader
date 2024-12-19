using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Utils.Structs;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogDropdownSelectViewModel : ViewModelBase{
    private readonly ContentDialog dialog;

    public ObservableCollection<StringItem> DropDownItemList{ get; } = new(){ };

    [ObservableProperty]
    private StringItem _selectedDropdownItem = new StringItem();

    [ObservableProperty]
    private string _episodeInfo;

    public ContentDialogDropdownSelectViewModel(ContentDialog dialog, string episodeInfo, List<string> dropdownItems){
        if (dialog is null){
            throw new ArgumentNullException(nameof(dialog));
        }

        this.dialog = dialog;
        dialog.Closed += DialogOnClosed;
        dialog.PrimaryButtonClick += SaveButton;
        EpisodeInfo = episodeInfo;
        foreach (var dropdownItem in dropdownItems){
            DropDownItemList.Add(new StringItem(){ stringValue = dropdownItem });
        }
    }

    private async void SaveButton(ContentDialog sender, ContentDialogButtonClickEventArgs args){
        dialog.PrimaryButtonClick -= SaveButton;
    }

    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}