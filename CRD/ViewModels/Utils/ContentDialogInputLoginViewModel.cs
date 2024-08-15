using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Structs;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogInputLoginViewModel : ViewModelBase{
    private readonly ContentDialog dialog;

    [ObservableProperty]
    private string _email;
    
    [ObservableProperty]
    private string _password;

    private AccountPageViewModel accountPageViewModel;

    public ContentDialogInputLoginViewModel(ContentDialog dialog, AccountPageViewModel accountPageViewModel){
        if (dialog is null){
            throw new ArgumentNullException(nameof(dialog));
        }

        this.dialog = dialog;
        dialog.Closed += DialogOnClosed;
        dialog.PrimaryButtonClick += LoginButton;
        this.accountPageViewModel = accountPageViewModel;
    }

    private async void LoginButton(ContentDialog sender, ContentDialogButtonClickEventArgs args){
        dialog.PrimaryButtonClick -= LoginButton;
        await CrunchyrollManager.Instance.CrAuth.Auth(new AuthData{Password = Password,Username = Email});
        accountPageViewModel.UpdatetProfile();
    }

    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}