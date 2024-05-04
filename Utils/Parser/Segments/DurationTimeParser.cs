using System;
using System.Collections.Generic;
using System.Linq;

namespace CRD.Utils.Parser.Segments;

public class DurationTimeParser{
    public static int? ParseEndNumber(string endNumber){
        if (!int.TryParse(endNumber, out var parsedEndNumber)){
            return null;
        }

        return parsedEndNumber;
    }

    public static dynamic GetSegmentRangeStatic(dynamic attributes){
        int timescale = attributes.timescale ?? 1;
        double segmentDuration = (double)attributes.duration / timescale;
        int? endNumber = ParseEndNumber(attributes.endNumber as string);

        if (endNumber.HasValue){
            return new{ start = 0, end = endNumber.Value };
        }

        if (attributes.periodDuration is double periodDuration){
            return new{ start = 0, end = (int)(periodDuration / segmentDuration) };
        }

        return new{ start = 0, end = (int)(attributes.sourceDuration / segmentDuration) };
    }

    public static dynamic GetSegmentRangeDynamic(dynamic attributes){
        long now = (attributes.NOW + attributes.clientOffset) / 1000;
        long periodStartWC = attributes.availabilityStartTime + attributes.periodStart;
        long periodEndWC = now + attributes.minimumUpdatePeriod;
        long periodDuration = periodEndWC - periodStartWC;
        int timescale = attributes.timescale ?? 1;
        int segmentCount = (int)Math.Ceiling(periodDuration * timescale / (double)attributes.duration);
        int availableStart = (int)Math.Floor((now - periodStartWC - attributes.timeShiftBufferDepth) * timescale / (double)attributes.duration);
        int availableEnd = (int)Math.Floor((now - periodStartWC) * timescale / (double)attributes.duration);

        int? endNumber = ParseEndNumber(attributes.endNumber as string);
        int end = endNumber.HasValue ? endNumber.Value : Math.Min(segmentCount, availableEnd);

        return new{ start = Math.Max(0, availableStart), end = end };
    }

    public static List<dynamic> ToSegments(dynamic attributes, int number){
        int timescale = attributes.timescale ?? 1;
        long periodStart = attributes.periodStart;
        int startNumber = attributes.startNumber ?? 1;

        return new List<dynamic>{
            new{
                number = startNumber + number,
                duration = (double)attributes.duration / timescale,
                timeline = periodStart,
                time = number * attributes.duration
            }
        };
    }

    public static IEnumerable<dynamic> ParseByDuration(dynamic attributes){
        var type = (string)attributes.type;
        var rangeFunction = type == "static" ? (Func<dynamic, dynamic>)GetSegmentRangeStatic : GetSegmentRangeDynamic;
        dynamic times = rangeFunction(attributes);
        List<int> d = Range(times.start, times.end - times.start);
        List<dynamic> segments = d.Select(number => ToSegments(attributes, number)).ToList();
        
        
        // Adjust the duration of the last segment for static type
        if (type == "static" && segments.Any()){
            var lastSegmentIndex = segments.Count - 1;
            double sectionDuration = attributes.periodDuration is double periodDuration ? periodDuration : attributes.sourceDuration;
            segments[lastSegmentIndex].duration = sectionDuration - ((double)attributes.duration / (attributes.timescale ?? 1) * lastSegmentIndex);
        }

        return segments;
    }

    public static List<int> Range(int start, int end){
        List<int> res = new List<int>();
        for (int i = start; i < end; i++){
            res.Add(i);
        }

        return res;
    }
}