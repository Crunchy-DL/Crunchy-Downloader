using System;

namespace CRD.Utils.Structs;

public class CrToken{
    public string? access_token { get; set; }
    public string? refresh_token { get; set; }
    public int? expires_in { get; set; }
    public string? token_type { get; set; }
    public string? scope { get; set; }
    public string? country { get; set; }
    public string? account_id { get; set; }
    public string? profile_id { get; set; }
    public DateTime? expires { get; set; }
}