using System;
using System.Collections.Generic;
using CRD.Utils.Parser.Utils;

namespace CRD.Utils.Parser.Segments;

public class TimelineTimeParser{
    public static int GetLiveRValue(dynamic attributes, long time, long duration){
        long now = (attributes.NOW + attributes.clientOffset) / 1000;
        long periodStartWC = attributes.availabilityStartTime + (attributes.periodStart ?? 0);
        long periodEndWC = now + (attributes.minimumUpdatePeriod ?? 0);
        long periodDuration = periodEndWC - periodStartWC;
        long timescale = attributes.timescale ?? 1;

        return (int)Math.Ceiling(((periodDuration * timescale) - time) / (double)duration);
    }

    public static List<dynamic> ParseByTimeline(dynamic attributes, IEnumerable<dynamic> segmentTimeline){
        var segments = new List<dynamic>();
        long time = -1;
        long timescale = attributes.timescale ?? 1;
        int startNumber = attributes.startNumber ?? 1;
        double timeline = attributes.periodStart;

        int sIndex = 0;
        foreach (var S in segmentTimeline){
            long duration = ObjectUtilities.GetMemberValue(S,"d");
            int repeat = ObjectUtilities.GetMemberValue(S,"r") ?? 0;
            long segmentTime = ObjectUtilities.GetMemberValue(S,"t") ?? 0;

            if (time < 0){
                // first segment
                time = segmentTime;
            }

            if (segmentTime > time){
                // discontinuity
                time = segmentTime;
            }

            int count;
            if (repeat < 0){
                count = GetLiveRValue(attributes, time, duration);
            } else{
                count = repeat + 1;
            }

            int end = startNumber + segments.Count + count;
            
            for (int number = startNumber + segments.Count; number < end; number++){
                segments.Add(new {
                    number = number,
                    duration = duration / (double)timescale,
                    time = time,
                    timeline = timeline
                });
                time += duration;
            }

            sIndex++;
        }

        return segments;
    }
}
