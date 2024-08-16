using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll;
using CRD.Views;
using Newtonsoft.Json;
using ReactiveUI;

namespace CRD.Downloader.Crunchyroll;

public class CrAuth{
    private readonly CrunchyrollManager crunInstance = CrunchyrollManager.Instance;

    public async Task AuthAnonymous(){
        var formData = new Dictionary<string, string>{
            { "grant_type", "client_id" },
            { "scope", "offline_access" }
        };
        var requestContent = new FormUrlEncodedContent(formData);
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        var request = new HttpRequestMessage(HttpMethod.Post, Api.BetaAuth){
            Content = requestContent
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Api.authBasicSwitch);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            JsonTokenToFileAndVariable(response.ResponseContent);
        } else{
            Console.Error.WriteLine("Anonymous login failed");
        }

        crunInstance.Profile = new CrProfile{
            Username = "???",
            Avatar = "003-cr-hime-excited.png",
            PreferredContentAudioLanguage = "ja-JP",
            PreferredContentSubtitleLanguage = "de-DE"
        };
    }

    private void JsonTokenToFileAndVariable(string content){
        crunInstance.Token = Helpers.Deserialize<CrToken>(content, crunInstance.SettingsJsonSerializerSettings);


        if (crunInstance.Token != null && crunInstance.Token.expires_in != null){
            crunInstance.Token.expires = DateTime.Now.AddSeconds((double)crunInstance.Token.expires_in);

            CfgManager.WriteTokenToYamlFile(crunInstance.Token, CfgManager.PathCrToken);
        }
    }

    public async Task Auth(AuthData data){
        var formData = new Dictionary<string, string>{
            { "username", data.Username },
            { "password", data.Password },
            { "grant_type", "password" },
            { "scope", "offline_access" }
        };
        var requestContent = new FormUrlEncodedContent(formData);
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        var request = new HttpRequestMessage(HttpMethod.Post, Api.BetaAuth){
            Content = requestContent
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Api.authBasicSwitch);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            JsonTokenToFileAndVariable(response.ResponseContent);
        } else{
            if (response.ResponseContent.Contains("invalid_credentials")){
                MessageBus.Current.SendMessage(new ToastMessage($"Failed to login - because of invalid login credentials", ToastType.Error, 10));
            } else{
                MessageBus.Current.SendMessage(new ToastMessage($"Failed to login - {response.ResponseContent.Substring(0, response.ResponseContent.Length < 200 ? response.ResponseContent.Length : 200)}",
                    ToastType.Error, 10));
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

        var request = HttpClientReq.CreateRequestMessage(Api.BetaProfile, HttpMethod.Get, true, true, null);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            var profileTemp = Helpers.Deserialize<CrProfile>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);

            if (profileTemp != null){
                crunInstance.Profile = profileTemp;

                var requestSubs = HttpClientReq.CreateRequestMessage(Api.Subscription + crunInstance.Token.account_id, HttpMethod.Get, true, false, null);

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
            return;
        }

        var formData = new Dictionary<string, string>{
            { "refresh_token", crunInstance.Token.refresh_token },
            { "grant_type", "refresh_token" },
            { "scope", "offline_access" }
        };
        var requestContent = new FormUrlEncodedContent(formData);
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        var request = new HttpRequestMessage(HttpMethod.Post, Api.BetaAuth){
            Content = requestContent
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Api.authBasicSwitch);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            JsonTokenToFileAndVariable(response.ResponseContent);
        } else{
            Console.Error.WriteLine("Token Auth Failed");
        }

        if (crunInstance.Token?.refresh_token != null){
            HttpClientReq.Instance.SetETPCookie(crunInstance.Token.refresh_token);

            await GetProfile();
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

        var formData = new Dictionary<string, string>{
            { "refresh_token", crunInstance.Token?.refresh_token ?? string.Empty },
            { "grant_type", "refresh_token" },
            { "scope", "offline_access" }
        };
        var requestContent = new FormUrlEncodedContent(formData);
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        var request = new HttpRequestMessage(HttpMethod.Post, Api.BetaAuth){
            Content = requestContent
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Api.authBasicSwitch);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            JsonTokenToFileAndVariable(response.ResponseContent);
        } else{
            Console.Error.WriteLine("Refresh Token Auth Failed");
        }
    }
}