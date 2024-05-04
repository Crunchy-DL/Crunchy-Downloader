using System;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class CrCmsToken{
    [JsonProperty("cms")] public CmsTokenB Cms{ get; set; }
    [JsonProperty("cms_beta")] public CmsTokenB CmsBeta{ get; set; }
    [JsonProperty("cms_web")] public CmsTokenB CmsWeb{ get; set; }

    [JsonProperty("service_available")] public bool ServiceAvailable{ get; set; }

    [JsonProperty("default_marketing_opt_in")]
    public bool DefaultMarketingOptIn{ get; set; }
}

public struct CmsTokenB{
    public string Bucket{ get; set; }
    public string Policy{ get; set; }
    public string Signature{ get; set; }
    [JsonProperty("key_pair_id")] public string KeyPairId{ get; set; }

    public DateTime Expires{ get; set; }
}