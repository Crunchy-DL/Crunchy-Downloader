using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Numerics;

namespace CRD.Utils.Parser.Segments;

public class SegmentBase{
    public static List<dynamic> SegmentsFromBase(dynamic attributes, List<dynamic> segmentTimeline){
        if (attributes.baseUrl == null){
            throw new Exception("NO_BASE_URL");
        }

        var initialization = attributes.initialization ?? new ExpandoObject();
        var sourceDuration = attributes.sourceDuration;
        var indexRange = attributes.indexRange ?? "";
        var periodStart = attributes.periodStart;
        var presentationTime = attributes.presentationTime;
        var number = attributes.number ?? 0;
        var duration = attributes.duration;

        dynamic initSegment = UrlType.UrlTypeToSegment(new{
            baseUrl = attributes.baseUrl,
            source = initialization.sourceURL,
            range = initialization.range
        });

        dynamic segment = UrlType.UrlTypeToSegment(new{
            baseUrl = attributes.baseUrl,
            source = attributes.baseUrl,
            indexRange = indexRange
        });

        segment.map = initSegment;

        if (duration != null){
            var segmentTimeInfo = DurationTimeParser.ParseByDuration(attributes);
            if (segmentTimeInfo.Count > 0){
                segment.duration = segmentTimeInfo[0].duration;
                segment.timeline = segmentTimeInfo[0].timeline;
            }
        } else if (sourceDuration != null){
            segment.duration = sourceDuration;
            segment.timeline = periodStart;
        }

        segment.presentationTime = presentationTime ?? periodStart;
        segment.number = number;

        return new List<dynamic>{ segment };
    }


    public static dynamic AddSidxSegmentsToPlaylist(dynamic playlist, dynamic sidx, string baseUrl){
        // Assume dynamic objects like sidx have properties similar to JavaScript objects
        var initSegment = playlist.sidx.ContainsKey("map") ? playlist.sidx.map : null;
        var sourceDuration = playlist.sidx.duration;
        var timeline = playlist.timeline ?? 0;
        dynamic sidxByteRange = playlist.sidx.byterange;
        BigInteger sidxEnd = new BigInteger((long)sidxByteRange.offset + (long)sidxByteRange.length);
        var timescale = (long)sidx.timescale;
        var mediaReferences = ((List<dynamic>)sidx.references).Where(r => r.referenceType != 1).ToList();
        var segments = new List<dynamic>();
        var type = playlist.endList ? "static" : "dynamic";
        var periodStart = (long)playlist.sidx.timeline;
        BigInteger presentationTime = new BigInteger(periodStart);
        var number = playlist.mediaSequence ?? 0;

        BigInteger startIndex;
        if (sidx.firstOffset is BigInteger){
            startIndex = sidxEnd + (BigInteger)sidx.firstOffset;
        } else{
            startIndex = sidxEnd + new BigInteger((long)sidx.firstOffset);
        }

        foreach (var reference in mediaReferences){
            var size = (long)reference.referencedSize;
            var duration = (long)reference.subsegmentDuration;
            BigInteger endIndex = startIndex + new BigInteger(size) - BigInteger.One;
            var indexRange = $"{startIndex}-{endIndex}";

            dynamic attributes = new ExpandoObject();
            attributes.baseUrl = baseUrl;
            attributes.timescale = timescale;
            attributes.timeline = timeline;
            attributes.periodStart = periodStart;
            attributes.presentationTime = (long)presentationTime;
            attributes.number = number;
            attributes.duration = duration;
            attributes.sourceDuration = sourceDuration;
            attributes.indexRange = indexRange;
            attributes.type = type;

            var segment = SegmentsFromBase(attributes, new List<dynamic>())[0];

            if (initSegment != null){
                segment.map = initSegment;
            }

            segments.Add(segment);
            startIndex += new BigInteger(size);
            presentationTime += new BigInteger(duration) / new BigInteger(timescale);
            number++;
        }

        playlist.segments = segments;

        return playlist;
    }
}