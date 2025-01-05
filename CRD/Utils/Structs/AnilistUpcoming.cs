using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public partial class AnilistSeries : ObservableObject{
    public int Id{ get; set; }
    public int? IdMal{ get; set; }
    public Title Title{ get; set; }
    public Date? StartDate{ get; set; }
    public Date EndDate{ get; set; }
    public string Status{ get; set; }
    public string Season{ get; set; }
    public string Format{ get; set; }
    public List<string> Genres{ get; set; }
    public List<string> Synonyms{ get; set; }
    public int? Duration{ get; set; }
    public int Popularity{ get; set; }
    public int? Episodes{ get; set; }
    public string Source{ get; set; }
    public string CountryOfOrigin{ get; set; }
    public string Hashtag{ get; set; }
    public int? AverageScore{ get; set; }
    public string SiteUrl{ get; set; }
    public string Description{ get; set; }
    public string BannerImage{ get; set; }
    public bool IsAdult{ get; set; }
    public CoverImage CoverImage{ get; set; }
    public Trailer Trailer{ get; set; }
    public List<ExternalLink>? ExternalLinks{ get; set; }
    public List<Ranking> Rankings{ get; set; }
    public Studios Studios{ get; set; }
    public Relations Relations{ get; set; }
    public AiringSchedule AiringSchedule{ get; set; }

    [JsonIgnore]
    public Bitmap? ThumbnailImage{ get; set; }

    [JsonIgnore]
    public string StartDateForm{
        get{
            if (StartDate == null)
                return string.Empty;


            var cultureInfo = System.Globalization.CultureInfo.InvariantCulture;
            string monthAbbreviation = cultureInfo.DateTimeFormat.GetAbbreviatedMonthName(StartDate.Month);

            return string.Format("{0:00}.{1}.{2}", StartDate.Day, monthAbbreviation, StartDate.Year);
        }
    }


    [JsonIgnore]
    public string? CrunchyrollID;

    [JsonIgnore]
    [ObservableProperty]
    public bool _hasCrID;

    [JsonIgnore]
    [ObservableProperty]
    public bool _isInHistory;
}

public class Title{
    public string Romaji{ get; set; }
    public string Native{ get; set; }
    public string English{ get; set; }
}

public class Date{
    public int Year{ get; set; }
    public int Month{ get; set; }
    public int Day{ get; set; }

    public DateTime? ToDateTime(){
        if (Year == 0 || Month == 0 || Day == 0)
            return DateTime.MinValue; 

        try{
            return new DateTime(Year, Month, Day);
        } catch{
            return DateTime.MinValue;; 
        }
    }
}

public class CoverImage{
    public string ExtraLarge{ get; set; }
    public string Color{ get; set; }
}

public class Trailer{
    public string Id{ get; set; }
    public string Site{ get; set; }
    public string Thumbnail{ get; set; }
}

public class ExternalLink{
    public string Site{ get; set; }
    public string Icon{ get; set; }
    public string Color{ get; set; }
    public string Url{ get; set; }
}

public class Ranking{
    public int Rank{ get; set; }
    public string Type{ get; set; }
    public string Season{ get; set; }
    public bool AllTime{ get; set; }
}

public class Studios{
    public List<StudioNode> Nodes{ get; set; }
}

public class StudioNode{
    public int Id{ get; set; }
    public string Name{ get; set; }
    public string SiteUrl{ get; set; }
}

public class Relations{
    public List<RelationEdge> Edges{ get; set; }
}

public class RelationEdge{
    public string RelationType{ get; set; }
    public RelationNode Node{ get; set; }
}

public class RelationNode{
    public int Id{ get; set; }
    public Title Title{ get; set; }
    public string SiteUrl{ get; set; }
}

public class AiringSchedule{
    public List<AiringNode> Nodes{ get; set; }
}

public class AiringNode{
    public int Episode{ get; set; }
    public long AiringAt{ get; set; }
}