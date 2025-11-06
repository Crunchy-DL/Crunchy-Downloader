using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Structs;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels.Utils;

public partial class ContentDialogInputLoginViewModel : ViewModelBase{
    private readonly ContentDialog dialog;

    private readonly TaskCompletionSource<bool> _loginTcs = new();

    public Task LoginCompleted => _loginTcs.Task;

    [ObservableProperty]
    private string _email;

    [ObservableProperty]
    private string _password;

    private AccountPageViewModel? accountPageViewModel;

    public ContentDialogInputLoginViewModel(ContentDialog dialog, AccountPageViewModel? accountPageViewModel = null){
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
        try{
            await CrunchyrollManager.Instance.CrAuthEndpoint1.Auth(new AuthData{ Password = Password, Username = Email });
            if (!string.IsNullOrEmpty(CrunchyrollManager.Instance.CrAuthEndpoint2.AuthSettings.Endpoint)){
                await CrunchyrollManager.Instance.CrAuthEndpoint2.Auth(new AuthData{ Password = Password, Username = Email });
            }
            
            accountPageViewModel?.UpdatetProfile();
            

            _loginTcs.TrySetResult(true);
        } catch (Exception ex){
            _loginTcs.TrySetException(ex);
        }
    }

    private void DialogOnClosed(ContentDialog sender, ContentDialogClosedEventArgs args){
        dialog.Closed -= DialogOnClosed;
    }
}