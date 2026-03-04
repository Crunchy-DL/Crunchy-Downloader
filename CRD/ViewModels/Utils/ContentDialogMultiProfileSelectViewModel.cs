using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Utils.UI;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogMultiProfileSelectViewModel: ViewModelBase{
    
    private readonly CustomContentDialog dialog;
    
    [ObservableProperty]
    private AccountProfile _selectedItem;

    [ObservableProperty]
    private ObservableCollection<AccountProfile> _profileList = [];
    
    public ContentDialogMultiProfileSelectViewModel(CustomContentDialog contentDialog, List<AccountProfile> profiles){
        ArgumentNullException.ThrowIfNull(contentDialog);

        dialog = contentDialog;
        dialog.Closed += DialogOnClosed;
        dialog.PrimaryButtonClick += SaveButton;

        try{
            _ = LoadProfiles(profiles);
        } catch (Exception e){
            Console.WriteLine(e);
        }
    }

    private async Task LoadProfiles(List<AccountProfile> profiles){
        foreach (var accountProfile in profiles){
            accountProfile.ProfileImage =  await Helpers.LoadImage(accountProfile.AvatarUrl);
            ProfileList.Add(accountProfile);
        }
    }

    partial void OnSelectedItemChanged(AccountProfile value){
        dialog.Hide(ContentDialogResult.Primary);
    }

    private void SaveButton(ContentDialog sender, ContentDialogButtonClickEventArgs args){
        dialog.PrimaryButtonClick -= SaveButton;
    }
    
    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}