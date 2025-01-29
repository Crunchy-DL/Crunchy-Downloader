using System.Collections.Generic;

namespace CRD.Utils.Structs.History;

public class SeriesDataCache{

    public string SeriesId{ get; set; } = "";
    
    public string SeriesTitle{ get; set; } = "";
    
    public string SeriesDescription{ get; set; } = "";
    public string ThumbnailImageUrl{ get; set; } = "";
    
    public List<string> HistorySeriesAvailableDubLang{ get; set; } =[];
    
    public List<string> HistorySeriesAvailableSoftSubs{ get; set; } =[];
}