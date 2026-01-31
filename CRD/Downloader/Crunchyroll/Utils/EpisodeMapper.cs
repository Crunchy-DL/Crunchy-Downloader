using System;
using System.Collections.Generic;
using System.Linq;
using CRD.Utils;
using CRD.Utils.Structs;

namespace CRD.Downloader.Crunchyroll.Utils;

public static class EpisodeMapper{
    public static CrunchyEpisode ToCrunchyEpisode(this CrBrowseEpisode src){
        if (src == null) throw new ArgumentNullException(nameof(src));

        var meta = src.EpisodeMetadata ?? new CrBrowseEpisodeMetaData();

        return new CrunchyEpisode{
   
            Id = src.Id ?? string.Empty,
            Slug = src.Slug ?? string.Empty,
            SlugTitle = src.SlugTitle ?? string.Empty,
            Title = src.Title ?? string.Empty,
            Description = src.Description ?? src.PromoDescription ?? string.Empty,
            MediaType = src.Type,
            ChannelId = src.ChannelId,
            StreamsLink = src.StreamsLink,
            Images = src.Images ?? new Images(),

 
            SeoTitle = src.PromoTitle ?? string.Empty,
            SeoDescription = src.PromoDescription ?? string.Empty,

    
            ProductionEpisodeId = src.ExternalId ?? string.Empty,
            ListingId = src.LinkedResourceKey ?? string.Empty,

  
            SeriesId = meta.SeriesId ?? string.Empty,
            SeasonId = meta.SeasonId ?? string.Empty,

            SeriesTitle = meta.SeriesTitle ?? string.Empty,
            SeriesSlugTitle = meta.SeriesSlugTitle ?? string.Empty,

            SeasonTitle = meta.SeasonTitle ?? string.Empty,
            SeasonSlugTitle = meta.SeasonSlugTitle ?? string.Empty,

            SeasonNumber = SafeInt(meta.SeasonNumber),
            SequenceNumber = (float)meta.SequenceNumber,

            Episode = meta.Episode,
            EpisodeNumber = meta.EpisodeCount,

            DurationMs = meta.DurationMs,
            Identifier = meta.Identifier ?? string.Empty,

            AvailabilityNotes = meta.AvailabilityNotes ?? string.Empty,
            EligibleRegion = meta.EligibleRegion ?? string.Empty,

            AvailabilityStarts = meta.AvailabilityStarts,
            AvailabilityEnds = meta.AvailabilityEnds,
            PremiumAvailableDate = meta.PremiumAvailableDate,
            FreeAvailableDate = meta.FreeAvailableDate,
            AvailableDate = meta.AvailableDate,
            PremiumDate = meta.PremiumDate,
            UploadDate = meta.UploadDate,
            EpisodeAirDate = meta.EpisodeAirDate,

            IsDubbed = meta.IsDubbed,
            IsSubbed = meta.IsSubbed,
            IsMature = meta.IsMature,
            IsClip = meta.IsClip,
            IsPremiumOnly = meta.IsPremiumOnly,
            MatureBlocked = meta.MatureBlocked,

            AvailableOffline = meta.AvailableOffline,
            ClosedCaptionsAvailable = meta.ClosedCaptionsAvailable,

            MaturityRatings = meta.MaturityRatings ?? new List<string>(),

      
            AudioLocale = (meta.AudioLocale ?? Locale.DefaulT).GetEnumMemberValue(),
            SubtitleLocales = (meta.SubtitleLocales ?? new List<Locale>())
                .Select(l => l.GetEnumMemberValue())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),

            ExtendedMaturityRating = ToStringKeyDict(meta.ExtendedMaturityRating),

            Versions = meta.versions?.Select(ToEpisodeVersion).ToList()
        };
    }

    private static EpisodeVersion ToEpisodeVersion(CrBrowseEpisodeVersion v){
        return new EpisodeVersion{
            AudioLocale = (v.AudioLocale ?? Locale.DefaulT).GetEnumMemberValue(),
            Guid = v.Guid ?? string.Empty,
            Original = v.Original,
            Variant = v.Variant ?? string.Empty,
            SeasonGuid = v.SeasonGuid ?? string.Empty,
            MediaGuid = v.MediaGuid,
            IsPremiumOnly = v.IsPremiumOnly,
            roles = Array.Empty<string>()
        };
    }

    private static int SafeInt(double value){
        if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static Dictionary<string, object> ToStringKeyDict(Dictionary<object, object>? dict){
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (dict == null) return result;

        foreach (var kv in dict){
            var key = kv.Key?.ToString();
            if (string.IsNullOrWhiteSpace(key)) continue;
            result[key] = kv.Value ?? new object();
        }

        return result;
    }
}