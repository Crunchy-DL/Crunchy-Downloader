using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
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

    private bool IsCancelled;
    private bool UnknownEndDate;
    private bool EndedButMaybeActive;

    public AccountPageViewModel(){
        UpdatetProfile();
    }

    private void Timer_Tick(object? sender, EventArgs e){
        var remaining = _targetTime - DateTime.Now;
        if (remaining <= TimeSpan.Zero){
            RemainingTime = "No active Subscription";
            _timer?.Stop();
            if (UnknownEndDate){
                RemainingTime = "Unknown Subscription end date";
            }

            if (EndedButMaybeActive){
                RemainingTime = "Subscription maybe ended";
            }

            if (CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Subscription != null){
                Console.Error.WriteLine(JsonConvert.SerializeObject(CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Subscription, Formatting.Indented));
            }
        } else{
            RemainingTime = $"{(IsCancelled ? "Subscription ending in: " : "Subscription refreshing in: ")}{remaining:dd\\:hh\\:mm\\:ss}";
        }
    }

    public void UpdatetProfile(){
        ProfileName = CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Username ?? CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.ProfileName ?? "???"; // Default or fetched user name
        LoginLogoutText = CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Username == "???" ? "Login" : "Logout"; // Default state
        LoadProfileImage("https://static.crunchyroll.com/assets/avatar/170x170/" +
                         (string.IsNullOrEmpty(CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Avatar) ? "crbrand_avatars_logo_marks_mangagirl_taupe.png" : CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Avatar));


        var subscriptions = CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Subscription;

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

            if (CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Subscription?.NextRenewalDate != null && !UnknownEndDate){
                _targetTime = CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Subscription.NextRenewalDate;
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

            if (CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Subscription != null){
                Console.Error.WriteLine(JsonConvert.SerializeObject(CrunchyrollManager.Instance.CrAuthEndpoint1.Profile.Subscription, Formatting.Indented));
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
            await CrunchyrollManager.Instance.CrAuthEndpoint1.AuthAnonymous();
            await CrunchyrollManager.Instance.CrAuthEndpoint2.AuthAnonymous();
            UpdatetProfile();
        }
    }

    public async void LoadProfileImage(string imageUrl){
        try{
            ProfileImage = await Helpers.LoadImage(imageUrl);
        } catch (Exception ex){
            // Handle exceptions
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
        }
    }
}