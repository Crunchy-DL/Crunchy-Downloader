using System;
using System.Collections.Generic;
using System.Linq;

namespace CRD.Utils.Parser.Segments;

public class SegmentList{
    public static List<dynamic> SegmentsFromList(dynamic attributes, List<dynamic> segmentTimeline){
        if ((!attributes.duration && segmentTimeline == null) ||
            (attributes.duration && segmentTimeline != null)){
            throw new Exception("Segment time unspecified");
        }

        List<dynamic> segmentUrls = ((List<dynamic>)attributes.segmentUrls)?.ToList() ?? new List<dynamic>();
        var segmentUrlMap = segmentUrls.Select(segmentUrlObject => SegmentURLToSegmentObject(attributes, segmentUrlObject)).ToList();

        List<dynamic> segmentTimeInfo = null;
        if (attributes.duration != null){
            segmentTimeInfo = DurationTimeParser.ParseByDuration(attributes); // Needs to be implemented
        } else if (segmentTimeline != null){
            segmentTimeInfo = TimelineTimeParser.ParseByTimeline(attributes, segmentTimeline); // Needs to be implemented
        }

        var segments = segmentTimeInfo.Select((segmentTime, index) => {
            if (index < segmentUrlMap.Count){
                var segment = segmentUrlMap[index];
                segment.Timeline = segmentTime.Timeline;
                segment.Duration = segmentTime.Duration;
                segment.Number = segmentTime.Number;
                segment.PresentationTime = attributes.periodStart + ((segmentTime.Time - (attributes.presentationTimeOffset ?? 0)) / (attributes.timescale ?? 1));

                return segment;
            }

            return null;
        }).Where(segment => segment != null).ToList();

        return segments;
    }

    public static dynamic SegmentURLToSegmentObject(dynamic attributes, dynamic segmentUrl){
        var initSegment = UrlType.UrlTypeToSegment(new{
            baseUrl = attributes.baseUrl,
            source = attributes.initialization?.sourceURL,
            range = attributes.initialization?.range
        });

        var segment = UrlType.UrlTypeToSegment(new{
            baseUrl = attributes.baseUrl,
            source = segmentUrl.media,
            range = segmentUrl.mediaRange
        });

        segment.Map = initSegment; 
        return segment;
    }
}