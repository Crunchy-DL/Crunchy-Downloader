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