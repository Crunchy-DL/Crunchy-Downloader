using System;
using System.Runtime.Serialization;
using CRD.Utils.JsonConv;
using Newtonsoft.Json;

namespace CRD.Utils;

[DataContract]
[JsonConverter(typeof(LocaleConverter))]
public enum Locale{
    [EnumMember(Value = "")]
    DefaulT,

    [EnumMember(Value = "un")]
    Unknown,

    [EnumMember(Value = "en-US")]
    EnUs,

    [EnumMember(Value = "es-LA")]
    EsLa,

    [EnumMember(Value = "es-419")]
    Es419,

    [EnumMember(Value = "es-ES")]
    EsEs,

    [EnumMember(Value = "pt-BR")]
    PtBr,

    [EnumMember(Value = "fr-FR")]
    FrFr,

    [EnumMember(Value = "de-DE")]
    DeDe,

    [EnumMember(Value = "ar-ME")]
    ArMe,

    [EnumMember(Value = "ar-SA")]
    ArSa,

    [EnumMember(Value = "it-IT")]
    ItIt,

    [EnumMember(Value = "ru-RU")]
    RuRu,

    [EnumMember(Value = "tr-TR")]
    TrTr,

    [EnumMember(Value = "hi-IN")]
    HiIn,

    [EnumMember(Value = "zh-CN")]
    ZhCn,

    [EnumMember(Value = "ko-KR")]
    KoKr,

    [EnumMember(Value = "ja-JP")]
    JaJp,

    [EnumMember(Value = "id-ID")]
    IdId,
    
    [EnumMember(Value = "en-IN")]
    EnIn,
    
    [EnumMember(Value = "pt-PT")]
    PtPt,
    
    [EnumMember(Value = "zh-TW")]
    ZhTw,
    
    [EnumMember(Value = "ca-ES")]
    CaEs,
    
    [EnumMember(Value = "pl-PL")]
    PlPl,
    
    [EnumMember(Value = "th-TH")]
    ThTh,
    
    [EnumMember(Value = "ta-IN")]
    TaIn,
    
    [EnumMember(Value = "ms-MY")]
    MsMy,
    
    [EnumMember(Value = "vi-VN")]
    ViVn,
    
    [EnumMember(Value = "te-IN")]
    TeIn,
    
    [EnumMember(Value = "id-ID")]
    idID,
}

public static class EnumExtensions{
    public static string GetEnumMemberValue(this Enum value){
        var type = value.GetType();
        var name = Enum.GetName(type, value);
        if (name != null){
            var field = type.GetField(name);
            if (field != null){
                var attr = Attribute.GetCustomAttribute(field, typeof(EnumMemberAttribute)) as EnumMemberAttribute;
                if (attr != null){
                    return attr.Value ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }
}

[DataContract]
public enum ChannelId{
    [EnumMember(Value = "crunchyroll")]
    Crunchyroll,
}

[DataContract]
public enum ImageType{
    [EnumMember(Value = "poster_tall")]
    PosterTall,

    [EnumMember(Value = "poster_wide")]
    PosterWide,

    [EnumMember(Value = "promo_image")]
    PromoImage,

    [EnumMember(Value = "thumbnail")]
    Thumbnail,
}

[DataContract]
public enum MaturityRating{
    [EnumMember(Value = "TV-14")]
    Tv14,
}

[DataContract]
public enum MediaType{
    [EnumMember(Value = "episode")]
    Episode,
}

[DataContract]
public enum DownloadMediaType{
    [EnumMember(Value = "Video")]
    Video,
    
    [EnumMember(Value = "SyncVideo")]
    SyncVideo,

    [EnumMember(Value = "Audio")]
    Audio,

    [EnumMember(Value = "Chapters")]
    Chapters,

    [EnumMember(Value = "Subtitle")]
    Subtitle,
    
    [EnumMember(Value = "Description")]
    Description,
}

public enum ScaledBorderAndShadowSelection{
    DontAdd,
    ScaledBorderAndShadowYes,
    ScaledBorderAndShadowNo,
}

public enum HistoryViewType{
    Posters,
    Table,
}

public enum SortingType{
    [EnumMember(Value = "Series Title")]
    SeriesTitle,
    [EnumMember(Value = "Next Air Date")]
    NextAirDate,
    [EnumMember(Value = "History Series Add Date")]
    HistorySeriesAddDate,
}

public enum SonarrCoverType{
    Banner,
    FanArt,
    Poster,
    ClearLogo,
}

public enum SonarrSeriesType{
    Anime,
    Standard,
    Daily
}

public enum SonarrStatus{
    Continuing,
    Upcoming,
    Ended,
    Deleted 
};