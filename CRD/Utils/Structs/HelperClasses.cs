using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Views;
using Newtonsoft.Json;

namespace CRD.Utils.Structs;

public class AuthData{
    public string Username{ get; set; }
    public string Password{ get; set; }
}

public class DrmAuthData{
    [JsonProperty("custom_data")]
    public string? CustomData{ get; set; }

    public string? Token{ get; set; }
}

public class Meta{
    [JsonProperty("versions_considered")]
    public bool? VersionsConsidered{ get; set; }
}

public class LanguageItem{
    [JsonProperty("cr_locale")]
    public string CrLocale{ get; set; }

    public string Locale{ get; set; }
    public string Code{ get; set; }
    public string Name{ get; set; }
    public string Language{ get; set; }
}

public class EpisodeAndLanguage{
    public List<CrunchyEpisode> Items{ get; set; }
    public List<LanguageItem> Langs{ get; set; }
}

public class CrunchyMultiDownload(List<string> dubLang, bool? all = null, bool? but = null, List<string>? e = null, string? s = null){
    public List<string> DubLang{ get; set; } = dubLang; //lang code
    public bool? AllEpisodes{ get; set; } = all; // download all episodes
    public bool? But{ get; set; } = but; //download all except selected episodes
    public List<string>? E{ get; set; } = e; //episode numbers
    public string? S{ get; set; } = s; //season id
}

public class CrunchySeriesList{
    public List<Episode> List{ get; set; }
    public Dictionary<string, EpisodeAndLanguage> Data{ get; set; }
}

public class Episode{
    public string E{ get; set; }
    public List<string> Lang{ get; set; }
    public string Name{ get; set; }
    public string Season{ get; set; }
    public string SeasonTitle{ get; set; }
    public string SeriesTitle{ get; set; }
    public string EpisodeNum{ get; set; }
    public string Id{ get; set; }
    public string Img{ get; set; }
    public string Description{ get; set; }
    public string Time{ get; set; }

    public EpisodeType EpisodeType{ get; set; } = EpisodeType.Unknown;
}

public class DownloadResponse{
    public List<DownloadedMedia> Data{ get; set; }
    public string? FileName{ get; set; }

    public string? FolderPath{ get; set; }
    public string? TempFolderPath{ get; set; }
    
    public string VideoTitle{ get; set; }
    public bool Error{ get; set; }
    public string ErrorText{ get; set; }
}

public class DownloadedMedia : SxItem{
    public DownloadMediaType Type{ get; set; }
    public LanguageItem Lang{ get; set; }
    public bool IsPrimary{ get; set; }

    public bool? Cc{ get; set; }
    public bool? Signs{ get; set; }

    public DownloadedMedia? RelatedVideoDownloadMedia;
}

public class SxItem{
    public LanguageItem Language{ get; set; }
    public string? Path{ get; set; }
    public string? File{ get; set; }
    public string? Title{ get; set; }
    public Dictionary<string, List<string>>? Fonts{ get; set; }
}

public class FrameData{
    public string FilePath{ get; set; }
    public double Time{ get; set; }
}

public class StringItem{
    public string stringValue{ get; set; }
}

public class WindowSettings
{
    public double Width { get; set; }
    public double Height { get; set; }
    public int ScreenIndex { get; set; }
    public int PosX { get; set; }
    public int PosY { get; set; }
    public bool IsMaximized { get; set; }
}

public class ToastMessage(string message, ToastType type, int i){
    public string? Message{ get; set; } = message;
    public int Seconds{ get; set; } = i;
    public ToastType Type{ get; set; } = type;
}

public class NavigationMessage{
    public Type? ViewModelType{ get; }
    public bool Back{ get; }
    public bool Refresh{ get; }

    public NavigationMessage(Type? viewModelType, bool back, bool refresh){
        ViewModelType = viewModelType;
        Back = back;
        Refresh = refresh;
    }
}

public partial class SeasonViewModel : ObservableObject{
    [ObservableProperty]
    private bool _isSelected;

    public string Season{ get; set; }
    public int Year{ get; set; }

    public string Display => $"{Season}\n{Year}";
}