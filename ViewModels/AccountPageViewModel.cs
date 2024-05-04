using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Views.Utils;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels;

public partial class AccountPageViewModel : ViewModelBase{
    [ObservableProperty] private Bitmap? _profileImage;

    [ObservableProperty] private string _profileName = "";

    [ObservableProperty] private string _loginLogoutText = "";


    public AccountPageViewModel(){
        UpdatetProfile();
    }

    public void UpdatetProfile(){
        ProfileName = Crunchyroll.Instance.Profile.Username; // Default or fetched user name
        LoginLogoutText = Crunchyroll.Instance.Profile.Username == "???" ? "Login" : "Logout"; // Default state
        LoadProfileImage("https://static.crunchyroll.com/assets/avatar/170x170/" + Crunchyroll.Instance.Profile.Avatar);
    }

    [RelayCommand]
    public async Task Button_Press(){
        if (LoginLogoutText == "Login"){
            var dialog = new ContentDialog(){
                Title = "Login",
                PrimaryButtonText = "Login",
                CloseButtonText = "Close"
            };

            var viewModel = new ContentDialogInputLoginViewModel(dialog, this);
            dialog.Content = new ContentDialogInputLoginView(){
                DataContext = viewModel
            };

            _ = await dialog.ShowAsync();
        } else{
            await Crunchyroll.Instance.CrAuth.AuthAnonymous();
            UpdatetProfile();
        }
    }

    public async void LoadProfileImage(string imageUrl){
        try{
            using (var client = new HttpClient()){
                var response = await client.GetAsync(imageUrl);
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync()){
                    ProfileImage = new Bitmap(stream);
                }
            }
        } catch (Exception ex){
            // Handle exceptions
            Console.WriteLine("Failed to load image: " + ex.Message);
        }
    }
}