using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRD.Downloader.Crunchyroll;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Utils.UI;
using CRD.ViewModels.Utils;
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
    private bool _hasMultiProfile;

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

            if (CrunchyrollManager.Instance.CrAuthEndpoint1.Subscription != null){
                Console.Error.WriteLine(JsonConvert.SerializeObject(CrunchyrollManager.Instance.CrAuthEndpoint1.Subscription, Formatting.Indented));
            }
        } else{
            RemainingTime = $"{(IsCancelled ? "Subscription ending in: " : "Subscription refreshing in: ")}{remaining:dd\\:hh\\:mm\\:ss}";
        }
    }

    public void UpdatetProfile(){
        
        var firstEndpoint = CrunchyrollManager.Instance.CrAuthEndpoint1;
        var firstEndpointProfile = firstEndpoint.Profile;
        
        HasMultiProfile = firstEndpoint.MultiProfile.Profiles.Count > 1;
        ProfileName = firstEndpointProfile.ProfileName ?? firstEndpointProfile.Username ?? "???"; // Default or fetched user name
        LoginLogoutText = firstEndpointProfile.Username == "???" ? "Login" : "Logout"; // Default state
        LoadProfileImage("https://static.crunchyroll.com/assets/avatar/170x170/" +
                         (string.IsNullOrEmpty(firstEndpointProfile.Avatar) ? "crbrand_avatars_logo_marks_mangagirl_taupe.png" : firstEndpointProfile.Avatar));


        var subscriptions = CrunchyrollManager.Instance.CrAuthEndpoint1.Subscription;

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

            if (!UnknownEndDate){
                _targetTime = subscriptions.NextRenewalDate;
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

            if (subscriptions != null){
                Console.Error.WriteLine(JsonConvert.SerializeObject(subscriptions, Formatting.Indented));
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

    [RelayCommand]
    public async Task OpenMultiProfileDialog(){
        var multiProfile = CrunchyrollManager.Instance.CrAuthEndpoint1.MultiProfile;

        var profiels = multiProfile.Profiles.Select(multiProfileProfile => new AccountProfile{
            AvatarUrl = string.IsNullOrEmpty(multiProfileProfile.Avatar) ? "" : ("https://static.crunchyroll.com/assets/avatar/170x170/" + multiProfileProfile.Avatar),
            ProfileName = multiProfileProfile.Username ?? multiProfileProfile.ProfileName ?? "???", CanBeSelected = multiProfileProfile is{ IsSelected: false, CanSwitch: true, IsPinProtected: false },
            ProfileId = multiProfileProfile.ProfileId,
        }).ToList();

        var dialog = new CustomContentDialog(){
            Name = "CRD Select Profile",
            Title = "Select Profile",
            IsPrimaryButtonEnabled = false,
            CloseButtonText = "Close",
            FullSizeDesired = true,
        };

        var viewModel = new ContentDialogMultiProfileSelectViewModel(dialog, profiels);
        dialog.Content = new ContentDialogMultiProfileSelectView(){
            DataContext = viewModel
        };

        var dialogResult = await dialog.ShowAsync();

        if (dialogResult == ContentDialogResult.Primary){
            var selectedProfile = viewModel.SelectedItem;
            
            await CrunchyrollManager.Instance.CrAuthEndpoint1.ChangeProfile(selectedProfile.ProfileId ?? string.Empty);
            await CrunchyrollManager.Instance.CrAuthEndpoint2.ChangeProfile(selectedProfile.ProfileId ?? string.Empty);
            
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