using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Structs;
using CRD.Views.Utils;
using FluentAvalonia.UI.Controls;
using Newtonsoft.Json;

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
    private bool UnknownEndDate = false;
    private bool EndedButMaybeActive = false;

    public AccountPageViewModel(){
        UpdatetProfile();
    }

    private void Timer_Tick(object sender, EventArgs e){
        var remaining = _targetTime - DateTime.Now;
        if (remaining <= TimeSpan.Zero){
            RemainingTime = "No active Subscription";
            _timer.Stop();
            if (UnknownEndDate){
                RemainingTime = "Unknown Subscription end date";
            }

            if (EndedButMaybeActive){
                RemainingTime = "Subscription maybe ended";
            }

            if (CrunchyrollManager.Instance.Profile.Subscription != null){
                Console.Error.WriteLine(JsonConvert.SerializeObject(CrunchyrollManager.Instance.Profile.Subscription, Formatting.Indented));
            }
        } else{
            RemainingTime = $"{(IsCancelled ? "Subscription ending in: " : "Subscription refreshing in: ")}{remaining:dd\\:hh\\:mm\\:ss}";
        }
    }

    public void UpdatetProfile(){
        ProfileName = CrunchyrollManager.Instance.Profile.Username ?? "???"; // Default or fetched user name
        LoginLogoutText = CrunchyrollManager.Instance.Profile.Username == "???" ? "Login" : "Logout"; // Default state
        LoadProfileImage("https://static.crunchyroll.com/assets/avatar/170x170/" + CrunchyrollManager.Instance.Profile.Avatar);


        var subscriptions = CrunchyrollManager.Instance.Profile.Subscription;

        if (subscriptions != null){
            if (subscriptions.SubscriptionProducts is{ Count: >= 1 }){
                var sub = subscriptions.SubscriptionProducts.First();
                IsCancelled = sub.IsCancelled;
                EndedButMaybeActive = !subscriptions.IsActive;
            } else if (subscriptions.ThirdPartySubscriptionProducts is{ Count: >= 1 }){
                var sub = subscriptions.ThirdPartySubscriptionProducts.First();
                IsCancelled = !sub.AutoRenew;
                EndedButMaybeActive = !subscriptions.IsActive;
            } else if (subscriptions.NonrecurringSubscriptionProducts is{ Count: >= 1 }){
                IsCancelled = true;
                EndedButMaybeActive = !subscriptions.IsActive;
            } else if (subscriptions.FunimationSubscriptions is{ Count: >= 1 }){
                IsCancelled = true;
                UnknownEndDate = true;
            }

            if (CrunchyrollManager.Instance.Profile.Subscription?.NextRenewalDate != null && !UnknownEndDate){
                _targetTime = CrunchyrollManager.Instance.Profile.Subscription.NextRenewalDate;
                _timer = new DispatcherTimer{
                    Interval = TimeSpan.FromSeconds(1)
                };
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
        } else{
            RemainingTime = "No active Subscription";
            if (_timer != null){
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
            }

            RaisePropertyChanged(nameof(RemainingTime));

            if (CrunchyrollManager.Instance.Profile.Subscription != null){
                Console.Error.WriteLine(JsonConvert.SerializeObject(CrunchyrollManager.Instance.Profile.Subscription, Formatting.Indented));
            }
        }

        if (UnknownEndDate){
            RemainingTime = "Unknown Subscription end date";
        }

        if (EndedButMaybeActive){
            RemainingTime = "Subscription maybe ended";
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

            var viewModel = new Utils.ContentDialogInputLoginViewModel(dialog, this);
            dialog.Content = new ContentDialogInputLoginView(){
                DataContext = viewModel
            };

            _ = await dialog.ShowAsync();
        } else{
            await CrunchyrollManager.Instance.CrAuth.AuthAnonymous();
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