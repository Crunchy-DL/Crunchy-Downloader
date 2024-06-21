using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class CrProfile{
    public string? Avatar{ get; set; }
    public string? Email{ get; set; }
    public string? Username{ get; set; }
    
    [JsonProperty("preferred_content_audio_language")]
    public string? PreferredContentAudioLanguage{ get; set; }
    
    [JsonProperty("preferred_content_subtitle_language")]
    public string? PreferredContentSubtitleLanguage{ get; set; }
    
    [JsonIgnore]
    public Subscription? Subscription{ get; set; }
    
    [JsonIgnore]
    public bool HasPremium{ get; set; }
}


public class Subscription{
    [JsonProperty("account_id")]
    public int AccountId{ get; set; }
    [JsonProperty("ctp_account_id")]
    public string? CtpAccountId{ get; set; }
    [JsonProperty("cycle_duration")]
    public string? CycleDuration{ get; set; }
    [JsonProperty("next_renewal_date")]
    public DateTime NextRenewalDate{ get; set; }
    [JsonProperty("currency_code")]
    public string? CurrencyCode{ get; set; }
    [JsonProperty("is_active")]
    public bool IsActive{ get; set; }
    [JsonProperty("tax_included")]
    public bool TaxIncluded{ get; set; }
    [JsonProperty("subscription_products")]
    public List<SubscriptionProduct>? SubscriptionProducts{ get; set; }
    
    [JsonProperty("third_party_subscription_products")]
    public List<ThirdPartySubscriptionProduct>? ThirdPartySubscriptionProducts{ get; set; }
    
    [JsonProperty("nonrecurring_subscription_products")]
    public List<NonRecurringSubscriptionProduct>? NonrecurringSubscriptionProducts{ get; set; }
}

public class NonRecurringSubscriptionProduct{
    [JsonProperty("start_date")]
    public DateTime StartDate{ get; set; }
    [JsonProperty("end_date")]
    public DateTime EndDate{ get; set; }
    public string? Sku{ get; set; }
    public string? Tier{ get; set; }
}


public class SubscriptionProduct{
    [JsonProperty("currency_code")]
    public string? CurrencyCode{ get; set; }
    public string? Amount{ get; set; }
    [JsonProperty("is_cancelled")]
    public bool IsCancelled{ get; set; }
    [JsonProperty("effective_date")]
    public DateTime EffectiveDate{ get; set; }
    public string? Sku{ get; set; }
    public string? Tier{ get; set; }
    [JsonProperty("active_free_trial")]
    public bool ActiveFreeTrial{ get; set; }
}

public class ThirdPartySubscriptionProduct{
    [JsonProperty("effective_date")]
    public DateTime EffectiveDate{ get; set; }
    public string? Source{ get; set; }
    [JsonProperty("source_reference")]
    public string? SourceReference{ get; set; }
    public string? Sku{ get; set; }
    public string? Tier{ get; set; }
    [JsonProperty("active_free_trial")]
    public bool ActiveFreeTrial{ get; set; }
    [JsonProperty("in_grace")]
    public bool InGrace{ get; set; }
    [JsonProperty("on_hold")]
    public bool OnHold{ get; set; }
    [JsonProperty("auto_renew")]
    public bool AutoRenew{ get; set; }
    [JsonProperty("expiration_date")]
    public DateTime ExpirationDate{ get; set; }

}