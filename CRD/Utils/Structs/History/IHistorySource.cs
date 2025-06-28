using System;
using System.Collections.Generic;

namespace CRD.Utils.Structs.History;

public interface IHistorySource{
    string GetSeriesId();
    string GetSeriesTitle();
    string GetSeasonTitle();
    string GetSeasonNum();
    string GetSeasonId();
    
    string GetImageUrl();
    
    string GetEpisodeId();
    string GetEpisodeNumber();
    string GetEpisodeTitle();
    string GetEpisodeDescription();
    
    bool IsSpecialSeason();
    bool IsSpecialEpisode();

    List<string> GetAnimeIds();
    
    List<string> GetEpisodeAvailableDubLang();
    List<string> GetEpisodeAvailableSoftSubs();

    DateTime GetAvailableDate();

    SeriesType GetSeriesType();
    EpisodeType GetEpisodeType();
}