using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Utils.Structs;
using CRD.Views.Utils;
using FluentAvalonia.UI.Controls;

namespace CRD.ViewModels;

public partial class AccountPageViewModel : ViewModelBase{
    [ObservableProperty]
    private Bitmap? _profileImage;

    [ObservableProperty]
    private string _profileName = "";

    [ObservableProperty]
    private string _loginLogoutText = "";

    [ObservableProperty]
    private string _remainingTime = "";

    private static DispatcherTimer? _timer;
    private DateTime _targetTime;
    private bool IsCancelled = false;

    public AccountPageViewModel(){
        UpdatetProfile();



    }

    private void Timer_Tick(object sender, EventArgs e){
        var remaining = _targetTime - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero){
            RemainingTime = "No active Subscription";
            _timer.Stop();
        } else{
            RemainingTime = $"{(IsCancelled ? "Subscription ending in: ":"Subscription refreshing in: ")}{remaining:dd\\:hh\\:mm\\:ss}";
        }
    }

    public void UpdatetProfile(){
        ProfileName = Crunchyroll.Instance.Profile.Username; // Default or fetched user name
        LoginLogoutText = Crunchyroll.Instance.Profile.Username == "???" ? "Login" : "Logout"; // Default state
        LoadProfileImage("https://static.crunchyroll.com/assets/avatar/170x170/" + Crunchyroll.Instance.Profile.Avatar);
        
        if (Crunchyroll.Instance.Profile.Subscription != null && Crunchyroll.Instance.Profile.Subscription?.SubscriptionProducts != null){
            var sub = Crunchyroll.Instance.Profile.Subscription?.SubscriptionProducts.First();
            _targetTime = Crunchyroll.Instance.Profile.Subscription.NextRenewalDate;
            if (sub != null){
                IsCancelled = sub.IsCancelled;
            }
            
            _timer = new DispatcherTimer{
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        } else{
            RemainingTime = "No active Subscription";
        }
        
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
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
        }
    }
}