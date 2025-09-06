using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CRD.Utils;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll;
using CRD.Views;
using Newtonsoft.Json;
using ReactiveUI;

namespace CRD.Downloader.Crunchyroll;

public class CrAuth(CrunchyrollManager crunInstance, CrAuthSettings authSettings){
    
    public CrToken? Token;
    public CrProfile Profile = new();

    public CrAuthSettings AuthSettings = authSettings;
    
    public Dictionary<string, CookieCollection> cookieStore = new();

    public void Init(){
        
        Profile = new CrProfile{
            Username = "???",
            Avatar = "crbrand_avatars_logo_marks_mangagirl_taupe.png",
            PreferredContentAudioLanguage = "ja-JP",
            PreferredContentSubtitleLanguage = crunInstance.DefaultLocale,
            HasPremium = false,
        };
        
    }

    private string GetTokenFilePath(){
        switch (AuthSettings.Endpoint){
            case "tv/samsung":
            case "tv/vidaa":
            case "tv/android_tv":
                return CfgManager.PathCrToken.Replace(".json", "_tv.json");
            case "android/phone":
            case "android/tablet":
                return CfgManager.PathCrToken.Replace(".json", "_android.json");
            case "console/switch":
            case "console/ps4":
            case "console/ps5":
            case "console/xbox_one":
                return CfgManager.PathCrToken.Replace(".json", "_console.json");
            default:
                return CfgManager.PathCrToken;
       
        }
    }

    public async Task Auth(){
        if (CfgManager.CheckIfFileExists(GetTokenFilePath())){
            Token = CfgManager.ReadJsonFromFile<CrToken>(GetTokenFilePath());
            await LoginWithToken();
        } else{
            await AuthAnonymous();
        }
    }
    
    public void SetETPCookie(string refreshToken){
        HttpClientReq.Instance.AddCookie(".crunchyroll.com", new Cookie("etp_rt", refreshToken),cookieStore);
        HttpClientReq.Instance.AddCookie(".crunchyroll.com", new Cookie("c_locale", "en-US"),cookieStore);
    }

    public async Task AuthAnonymous(){
        string uuid = Guid.NewGuid().ToString();

        var formData = new Dictionary<string, string>{
            { "grant_type", "client_id" },
            { "scope", "offline_access" },
            { "device_id", uuid },
            { "device_type", AuthSettings.Device_type },
        };

        if (!string.IsNullOrEmpty(AuthSettings.Device_name)){
            formData.Add("device_name", AuthSettings.Device_name);
        }

        var requestContent = new FormUrlEncodedContent(formData);

        var crunchyAuthHeaders = new Dictionary<string, string>{
            { "Authorization", AuthSettings.Authorization },
            { "User-Agent", AuthSettings.UserAgent }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrls.Auth){
            Content = requestContent
        };

        foreach (var header in crunchyAuthHeaders){
            request.Headers.Add(header.Key, header.Value);
        }

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            JsonTokenToFileAndVariable(response.ResponseContent, uuid);
        } else{
            Console.Error.WriteLine("Anonymous login failed");
        }

        Profile = new CrProfile{
            Username = "???",
            Avatar = "crbrand_avatars_logo_marks_mangagirl_taupe.png",
            PreferredContentAudioLanguage = "ja-JP",
            PreferredContentSubtitleLanguage = "de-DE"
        };
    }

    private void JsonTokenToFileAndVariable(string content, string deviceId){
        Token = Helpers.Deserialize<CrToken>(content, crunInstance.SettingsJsonSerializerSettings);

        if (Token is{ expires_in: not null }){
            Token.device_id = deviceId;
            Token.expires = DateTime.Now.AddSeconds((double)Token.expires_in);

            CfgManager.WriteJsonToFile(GetTokenFilePath(), Token);
        }
    }

    public async Task Auth(AuthData data){
        string uuid = Guid.NewGuid().ToString();

        var formData = new Dictionary<string, string>{
            { "username", data.Username },
            { "password", data.Password },
            { "grant_type", "password" },
            { "scope", "offline_access" },
            { "device_id", uuid },
            { "device_type", AuthSettings.Device_type },
        };

        if (!string.IsNullOrEmpty(AuthSettings.Device_name)){
            formData.Add("device_name", AuthSettings.Device_name);
        }

        var requestContent = new FormUrlEncodedContent(formData);

        var crunchyAuthHeaders = new Dictionary<string, string>{
            { "Authorization", AuthSettings.Authorization },
            { "User-Agent", AuthSettings.UserAgent }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrls.Auth){
            Content = requestContent
        };

        foreach (var header in crunchyAuthHeaders){
            request.Headers.Add(header.Key, header.Value);
        }

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            JsonTokenToFileAndVariable(response.ResponseContent, uuid);
        } else{
            if (response.ResponseContent.Contains("invalid_credentials")){
                MessageBus.Current.SendMessage(new ToastMessage($"Failed to login - because of invalid login credentials", ToastType.Error, 5));
            } else if (response.ResponseContent.Contains("<title>Just a moment...</title>") || 
                       response.ResponseContent.Contains("<title>Access denied</title>") || 
                       response.ResponseContent.Contains("<title>Attention Required! | Cloudflare</title>") || 
                       response.ResponseContent.Trim().Equals("error code: 1020") || 
                       response.ResponseContent.IndexOf("<title>DDOS-GUARD</title>", StringComparison.OrdinalIgnoreCase) > -1){
                MessageBus.Current.SendMessage(new ToastMessage($"Failed to login - Cloudflare error try to change to BetaAPI in settings", ToastType.Error, 5));
            } else{
                MessageBus.Current.SendMessage(new ToastMessage($"Failed to login - {response.ResponseContent.Substring(0, response.ResponseContent.Length < 200 ? response.ResponseContent.Length : 200)}",
                    ToastType.Error, 5));
                await Console.Error.WriteLineAsync("Full Response: " + response.ResponseContent);
            }
        }

        if (Token?.refresh_token != null){
            SetETPCookie(Token.refresh_token);

            await GetProfile();
        }
    }

    public async Task GetProfile(){
        if (Token?.access_token == null){
            Console.Error.WriteLine("Missing Access Token");
            return;
        }

        var request = HttpClientReq.CreateRequestMessage(ApiUrls.Profile, HttpMethod.Get, true, Token.access_token, null);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            var profileTemp = Helpers.Deserialize<CrProfile>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

            if (profileTemp != null){
                Profile = profileTemp;

                var requestSubs = HttpClientReq.CreateRequestMessage(ApiUrls.Subscription + Token.account_id, HttpMethod.Get, true, Token.access_token, null);

                var responseSubs = await HttpClientReq.Instance.SendHttpRequest(requestSubs);

                if (responseSubs.IsOk){
                    var subsc = Helpers.Deserialize<Subscription>(responseSubs.ResponseContent, crunInstance.SettingsJsonSerializerSettings);
                    Profile.Subscription = subsc;
                    if (subsc is{ SubscriptionProducts:{ Count: 0 }, ThirdPartySubscriptionProducts.Count: > 0 }){
                        var thirdPartySub = subsc.ThirdPartySubscriptionProducts.First();
                        var expiration = thirdPartySub.InGrace ? thirdPartySub.InGraceExpirationDate : thirdPartySub.ExpirationDate;
                        var remaining = expiration - DateTime.Now;
                        Profile.HasPremium = true;
                        if (Profile.Subscription != null){
                            Profile.Subscription.IsActive = remaining > TimeSpan.Zero;
                            Profile.Subscription.NextRenewalDate = expiration;
                        }
                    } else if (subsc is{ SubscriptionProducts:{ Count: 0 }, NonrecurringSubscriptionProducts.Count: > 0 }){
                        var nonRecurringSub = subsc.NonrecurringSubscriptionProducts.First();
                        var remaining = nonRecurringSub.EndDate - DateTime.Now;
                        Profile.HasPremium = true;
                        if (Profile.Subscription != null){
                            Profile.Subscription.IsActive = remaining > TimeSpan.Zero;
                            Profile.Subscription.NextRenewalDate = nonRecurringSub.EndDate;
                        }
                    } else if (subsc is{ SubscriptionProducts:{ Count: 0 }, FunimationSubscriptions.Count: > 0 }){
                        Profile.HasPremium = true;
                    } else if (subsc is{ SubscriptionProducts.Count: > 0 }){
                        Profile.HasPremium = true;
                    } else{
                        Profile.HasPremium = false;
                        Console.Error.WriteLine($"No subscription available:\n {JsonConvert.SerializeObject(subsc, Formatting.Indented)} ");
                    }
                } else{
                    Profile.HasPremium = false;
                    Console.Error.WriteLine("Failed to check premium subscription status");
                }
            }
        }
    }

    public async Task LoginWithToken(){
        if (Token?.refresh_token == null){
            Console.Error.WriteLine("Missing Refresh Token");
            await AuthAnonymous();
            return;
        }

        string uuid = string.IsNullOrEmpty(Token.device_id) ? Guid.NewGuid().ToString() : Token.device_id;

        var formData = new Dictionary<string, string>{
            { "refresh_token", Token.refresh_token },
            { "scope", "offline_access" },
            { "device_id", uuid },
            { "grant_type", "refresh_token" },
            { "device_type", AuthSettings.Device_type },
        };

        if (!string.IsNullOrEmpty(AuthSettings.Device_name)){
            formData.Add("device_name", AuthSettings.Device_name);
        }

        var requestContent = new FormUrlEncodedContent(formData);

        var crunchyAuthHeaders = new Dictionary<string, string>{
            { "Authorization", AuthSettings.Authorization },
            { "User-Agent", AuthSettings.UserAgent }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrls.Auth){
            Content = requestContent
        };

        foreach (var header in crunchyAuthHeaders){
            request.Headers.Add(header.Key, header.Value);
        }

        SetETPCookie(Token.refresh_token);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.ResponseContent.Contains("<title>Just a moment...</title>") || 
            response.ResponseContent.Contains("<title>Access denied</title>") || 
            response.ResponseContent.Contains("<title>Attention Required! | Cloudflare</title>") || 
            response.ResponseContent.Trim().Equals("error code: 1020") || 
            response.ResponseContent.IndexOf("<title>DDOS-GUARD</title>", StringComparison.OrdinalIgnoreCase) > -1){
            MessageBus.Current.SendMessage(new ToastMessage($"Failed to login - Cloudflare error try to change to BetaAPI in settings", ToastType.Error, 5));
            Console.Error.WriteLine($"Failed to login - Cloudflare error try to change to BetaAPI in settings");
        }
        
        if (response.IsOk){
            JsonTokenToFileAndVariable(response.ResponseContent, uuid);

            if (Token?.refresh_token != null){
                SetETPCookie(Token.refresh_token);

                await GetProfile();
            }
        } else{
            Console.Error.WriteLine("Token Auth Failed");
            await AuthAnonymous();
            
            MainWindow.Instance.ShowError("Login failed. Please check the log for more details.");
            
        }
    }

    public async Task RefreshToken(bool needsToken){
        if (Token?.access_token == null && Token?.refresh_token == null ||
            Token.access_token != null && Token.refresh_token == null){
            await AuthAnonymous();
        } else{
            if (!(DateTime.Now > Token.expires) && needsToken){
                return;
            }
        }

        if (Profile.Username == "???"){
            return;
        }

        string uuid = string.IsNullOrEmpty(Token?.device_id) ? Guid.NewGuid().ToString() : Token.device_id;

        var formData = new Dictionary<string, string>{
            { "refresh_token", Token?.refresh_token ?? "" },
            { "grant_type", "refresh_token" },
            { "scope", "offline_access" },
            { "device_id", uuid },
            { "device_type", AuthSettings.Device_type },
        };

        if (!string.IsNullOrEmpty(AuthSettings.Device_name)){
            formData.Add("device_name", AuthSettings.Device_name);
        }

        var requestContent = new FormUrlEncodedContent(formData);

        var crunchyAuthHeaders = new Dictionary<string, string>{
            { "Authorization", AuthSettings.Authorization },
            { "User-Agent", AuthSettings.UserAgent }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrls.Auth){
            Content = requestContent
        };

        foreach (var header in crunchyAuthHeaders){
            request.Headers.Add(header.Key, header.Value);
        }

        SetETPCookie(Token?.refresh_token ?? string.Empty);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            JsonTokenToFileAndVariable(response.ResponseContent, uuid);
        } else{
            Console.Error.WriteLine("Refresh Token Auth Failed");
        }
    }
}