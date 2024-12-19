using System.Collections.Generic;

namespace CRD.Utils.Structs.History;

public class AniListResponse{
    public Data? Data{ get; set; }
}

public class Data{
    public Page? Page{ get; set; }
}

public class Page{
    public PageInfo? PageInfo{ get; set; }
    public List<AnilistSeries>? Media{ get; set; }
}

public class PageInfo{
    public bool HasNextPage{ get; set; }
    public int Total{ get; set; }
}

public class AniListResponseCalendar{
    public Data2? Data{ get; set; }
}

public class Data2{
    public Page2? Page{ get; set; }
}

public class Page2{
    public PageInfo? PageInfo{ get; set; }
    public List<AiringSchedule>? AiringSchedules{ get; set; }
}

public class AiringSchedule{
    public int Id{ get; set; }
    public int Episode{ get; set; }
    public int AiringAt{ get; set; }
    public AnilistSeries? Media{ get; set; }
}