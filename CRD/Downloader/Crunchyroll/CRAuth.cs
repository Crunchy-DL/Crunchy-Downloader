using System;
using System.Collections.Generic;
using System.Linq;
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

public class CrAuth{
    private readonly CrunchyrollManager crunInstance = CrunchyrollManager.Instance;

    private readonly string authorization = ApiUrls.authBasicMob;
    private readonly string userAgent = ApiUrls.MobileUserAgent;
    private readonly string deviceType = "OnePlus CPH2449";
    private readonly string deviceName = "CPH2449";

    public async Task AuthAnonymous(){
        string uuid = Guid.NewGuid().ToString();

        var formData = new Dictionary<string, string>{
            { "grant_type", "client_id" },
            { "scope", "offline_access" },
            { "device_id", uuid },
            { "device_type", deviceType },
        };

        if (!string.IsNullOrEmpty(deviceName)){
            formData.Add("device_name", deviceName);
        }

        var requestContent = new FormUrlEncodedContent(formData);

        var crunchyAuthHeaders = new Dictionary<string, string>{
            { "Authorization", authorization },
            { "User-Agent", userAgent }
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

        crunInstance.Profile = new CrProfile{
            Username = "???",
            Avatar = "crbrand_avatars_logo_marks_mangagirl_taupe.png",
            PreferredContentAudioLanguage = "ja-JP",
            PreferredContentSubtitleLanguage = "de-DE"
        };
    }

    private void JsonTokenToFileAndVariable(string content, string deviceId){
        crunInstance.Token = Helpers.Deserialize<CrToken>(content, crunInstance.SettingsJsonSerializerSettings);

        if (crunInstance.Token is{ expires_in: not null }){
            crunInstance.Token.device_id = deviceId;
            crunInstance.Token.expires = DateTime.Now.AddSeconds((double)crunInstance.Token.expires_in);

            CfgManager.WriteJsonToFile(CfgManager.PathCrToken, crunInstance.Token);
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
            { "device_type", deviceType },
        };

        if (!string.IsNullOrEmpty(deviceName)){
            formData.Add("device_name", deviceName);
        }

        var requestContent = new FormUrlEncodedContent(formData);

        var crunchyAuthHeaders = new Dictionary<string, string>{
            { "Authorization", authorization },
            { "User-Agent", userAgent }
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

        if (crunInstance.Token?.refresh_token != null){
            HttpClientReq.Instance.SetETPCookie(crunInstance.Token.refresh_token);

            await GetProfile();
        }
    }

    public async Task GetProfile(){
        if (crunInstance.Token?.access_token == null){
            Console.Error.WriteLine("Missing Access Token");
            return;
        }

        var request = HttpClientReq.CreateRequestMessage(ApiUrls.Profile, HttpMethod.Get, true, true, null);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            var profileTemp = Helpers.Deserialize<CrProfile>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

            if (profileTemp != null){
                crunInstance.Profile = profileTemp;

                var requestSubs = HttpClientReq.CreateRequestMessage(ApiUrls.Subscription + crunInstance.Token.account_id, HttpMethod.Get, true, false, null);

                var responseSubs = await HttpClientReq.Instance.SendHttpRequest(requestSubs);

                if (responseSubs.IsOk){
                    var subsc = Helpers.Deserialize<Subscription>(responseSubs.ResponseContent, crunInstance.SettingsJsonSerializerSettings);
                    crunInstance.Profile.Subscription = subsc;
                    if (subsc is{ SubscriptionProducts:{ Count: 0 }, ThirdPartySubscriptionProducts.Count: > 0 }){
                        var thirdPartySub = subsc.ThirdPartySubscriptionProducts.First();
                        var expiration = thirdPartySub.InGrace ? thirdPartySub.InGraceExpirationDate : thirdPartySub.ExpirationDate;
                        var remaining = expiration - DateTime.Now;
                        crunInstance.Profile.HasPremium = true;
                        if (crunInstance.Profile.Subscription != null){
                            crunInstance.Profile.Subscription.IsActive = remaining > TimeSpan.Zero;
                            crunInstance.Profile.Subscription.NextRenewalDate = expiration;
                        }
                    } else if (subsc is{ SubscriptionProducts:{ Count: 0 }, NonrecurringSubscriptionProducts.Count: > 0 }){
                        var nonRecurringSub = subsc.NonrecurringSubscriptionProducts.First();
                        var remaining = nonRecurringSub.EndDate - DateTime.Now;
                        crunInstance.Profile.HasPremium = true;
                        if (crunInstance.Profile.Subscription != null){
                            crunInstance.Profile.Subscription.IsActive = remaining > TimeSpan.Zero;
                            crunInstance.Profile.Subscription.NextRenewalDate = nonRecurringSub.EndDate;
                        }
                    } else if (subsc is{ SubscriptionProducts:{ Count: 0 }, FunimationSubscriptions.Count: > 0 }){
                        crunInstance.Profile.HasPremium = true;
                    } else if (subsc is{ SubscriptionProducts.Count: > 0 }){
                        crunInstance.Profile.HasPremium = true;
                    } else{
                        crunInstance.Profile.HasPremium = false;
                        Console.Error.WriteLine($"No subscription available:\n {JsonConvert.SerializeObject(subsc, Formatting.Indented)} ");
                    }
                } else{
                    crunInstance.Profile.HasPremium = false;
                    Console.Error.WriteLine("Failed to check premium subscription status");
                }
            }
        }
    }

    public async Task LoginWithToken(){
        if (crunInstance.Token?.refresh_token == null){
            Console.Error.WriteLine("Missing Refresh Token");
            await AuthAnonymous();
            return;
        }

        string uuid = string.IsNullOrEmpty(crunInstance.Token.device_id) ? Guid.NewGuid().ToString() : crunInstance.Token.device_id;

        var formData = new Dictionary<string, string>{
            { "refresh_token", crunInstance.Token.refresh_token },
            { "scope", "offline_access" },
            { "device_id", uuid },
            { "grant_type", "refresh_token" },
            { "device_type", deviceType },
        };

        if (!string.IsNullOrEmpty(deviceName)){
            formData.Add("device_name", deviceName);
        }

        var requestContent = new FormUrlEncodedContent(formData);

        var crunchyAuthHeaders = new Dictionary<string, string>{
            { "Authorization", authorization },
            { "User-Agent", userAgent }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrls.Auth){
            Content = requestContent
        };

        foreach (var header in crunchyAuthHeaders){
            request.Headers.Add(header.Key, header.Value);
        }

        HttpClientReq.Instance.SetETPCookie(crunInstance.Token.refresh_token);

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

            if (crunInstance.Token?.refresh_token != null){
                HttpClientReq.Instance.SetETPCookie(crunInstance.Token.refresh_token);

                await GetProfile();
            }
        } else{
            Console.Error.WriteLine("Token Auth Failed");
            await AuthAnonymous();
            
            MainWindow.Instance.ShowError("Login failed. Please check the log for more details.");
            
        }
    }

    public async Task RefreshToken(bool needsToken){
        if (crunInstance.Token?.access_token == null && crunInstance.Token?.refresh_token == null ||
            crunInstance.Token.access_token != null && crunInstance.Token.refresh_token == null){
            await AuthAnonymous();
        } else{
            if (!(DateTime.Now > crunInstance.Token.expires) && needsToken){
                return;
            }
        }

        if (crunInstance.Profile.Username == "???"){
            return;
        }

        string uuid = string.IsNullOrEmpty(crunInstance.Token?.device_id) ? Guid.NewGuid().ToString() : crunInstance.Token.device_id;

        var formData = new Dictionary<string, string>{
            { "refresh_token", crunInstance.Token?.refresh_token ?? "" },
            { "grant_type", "refresh_token" },
            { "scope", "offline_access" },
            { "device_id", uuid },
            { "device_type", deviceType },
        };

        if (!string.IsNullOrEmpty(deviceName)){
            formData.Add("device_name", deviceName);
        }

        var requestContent = new FormUrlEncodedContent(formData);

        var crunchyAuthHeaders = new Dictionary<string, string>{
            { "Authorization", authorization },
            { "User-Agent", userAgent }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrls.Auth){
            Content = requestContent
        };

        foreach (var header in crunchyAuthHeaders){
            request.Headers.Add(header.Key, header.Value);
        }

        HttpClientReq.Instance.SetETPCookie(crunInstance.Token?.refresh_token ?? string.Empty);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            JsonTokenToFileAndVariable(response.ResponseContent, uuid);
        } else{
            Console.Error.WriteLine("Refresh Token Auth Failed");
        }
    }
}