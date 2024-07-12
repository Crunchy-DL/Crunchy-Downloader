using System.Collections.Generic;

namespace CRD.Utils.Structs;

public class CrSearchSeries{
    public int count{ get; set; }
    public List<CrBrowseSeries>? Items{ get; set; }
    public string? type{ get; set; }
}

public class CrSearchSeriesBase{
    public int Total{ get; set; }
    public List<CrSearchSeries>? Data{ get; set; }
    public Meta Meta{ get; set; }
}