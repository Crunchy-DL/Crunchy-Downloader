using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using CRD.Utils;
using CRD.Utils.Structs;
using Newtonsoft.Json;
using YamlDotNet.Core.Tokens;

namespace CRD.Downloader;

public class CrAuth{

    private readonly Crunchyroll crunInstance = Crunchyroll.Instance;

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

        Crunchyroll.Instance.CmsToken = new CrCmsToken();

    }

    private void JsonTokenToFileAndVariable(string content){
        crunInstance.Token = JsonConvert.DeserializeObject<CrToken>(content, crunInstance.SettingsJsonSerializerSettings);


        if (crunInstance.Token != null && crunInstance.Token.expires_in != null){
            crunInstance.Token.expires = DateTime.Now.AddMilliseconds((double)crunInstance.Token.expires_in);

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
                    if ( subsc is{ SubscriptionProducts:{ Count: 0 }, ThirdPartySubscriptionProducts.Count: > 0 }){
                        var thirdPartySub = subsc.ThirdPartySubscriptionProducts.First();
                        var remaining = thirdPartySub.ExpirationDate - DateTime.UtcNow;
                        crunInstance.Profile.HasPremium = remaining > TimeSpan.Zero;
                        crunInstance.Profile.Subscription.IsActive = remaining > TimeSpan.Zero;
                        crunInstance.Profile.Subscription.NextRenewalDate = thirdPartySub.ExpirationDate;
                    } else if(subsc is{ SubscriptionProducts:{ Count: 0 }, NonrecurringSubscriptionProducts.Count: > 0 }){
                        var nonRecurringSub = subsc.NonrecurringSubscriptionProducts.First();
                        var remaining = nonRecurringSub.EndDate - DateTime.UtcNow;
                        crunInstance.Profile.HasPremium = remaining > TimeSpan.Zero;
                        crunInstance.Profile.Subscription.IsActive = remaining > TimeSpan.Zero;
                        crunInstance.Profile.Subscription.NextRenewalDate = nonRecurringSub.EndDate;
                    } else{
                        crunInstance.Profile.HasPremium = subsc.IsActive; 
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

        await GetCmsToken();
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

        await GetCmsToken();
    }


    public async Task GetCmsToken(){
        if (crunInstance.Token?.access_token == null){
            Console.Error.WriteLine($"Missing Access Token: {crunInstance.Token?.access_token}");
            return;
        }

        var request = HttpClientReq.CreateRequestMessage(Api.BetaCmsToken, HttpMethod.Get, true, true, null);

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            crunInstance.CmsToken = JsonConvert.DeserializeObject<CrCmsToken>(response.ResponseContent, crunInstance.SettingsJsonSerializerSettings);
        } else{
            Console.Error.WriteLine("CMS Token Auth Failed");
        }
    }

    public async Task GetCmsData(){
        if (crunInstance.CmsToken?.Cms == null){
            Console.Error.WriteLine("Missing CMS Token");
            return;
        }

        UriBuilder uriBuilder = new UriBuilder(Api.BetaCms + crunInstance.CmsToken.Cms.Bucket + "/index?");

        NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);

        query["preferred_audio_language"] = "ja-JP";
        query["Policy"] = crunInstance.CmsToken.Cms.Policy;
        query["Signature"] = crunInstance.CmsToken.Cms.Signature;
        query["Key-Pair-Id"] = crunInstance.CmsToken.Cms.KeyPairId;

        uriBuilder.Query = query.ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.ToString());

        var response = await HttpClientReq.Instance.SendHttpRequest(request);

        if (response.IsOk){
            Console.WriteLine(response.ResponseContent);
        } else{
            Console.Error.WriteLine("Failed to Get CMS Index");
        }
    }
}