using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using CRD.Utils.Parser.Utils;

namespace CRD.Utils.Parser.Segments;

public class SegmentTemplate{
    public static List<dynamic> SegmentsFromTemplate(dynamic attributes, List<dynamic> segmentTimeline){
        dynamic templateValues = new ExpandoObject();
        templateValues.RepresentationID = ObjectUtilities.GetMemberValue(attributes,"id");
        templateValues.Bandwidth = ObjectUtilities.GetMemberValue(attributes,"bandwidth") ?? 0;

        dynamic initialization = attributes.initialization ?? new{ sourceURL = string.Empty, range = string.Empty };

        dynamic mapSegment = UrlType.UrlTypeToSegment(new{
            baseUrl = ObjectUtilities.GetMemberValue(attributes,"baseUrl"),
            source = ConstructTemplateUrl(initialization.sourceURL, templateValues),
            range = ObjectUtilities.GetMemberValue(initialization,"range")
        });

        List<dynamic> segments = ParseTemplateInfo(attributes, segmentTimeline);

        return segments.Select(segment => {
            templateValues.Number = ObjectUtilities.GetMemberValue(segment,"number");
            templateValues.Time = ObjectUtilities.GetMemberValue(segment,"time");

            var uri = ConstructTemplateUrl(ObjectUtilities.GetMemberValue(attributes,"media") ?? "", templateValues);
            var timescale = ObjectUtilities.GetMemberValue(attributes,"timescale") ?? 1;
            var presentationTimeOffset = ObjectUtilities.GetMemberValue(attributes,"presentationTimeOffset") ?? 0;
            double presentationTime = ObjectUtilities.GetMemberValue(attributes,"periodStart") + ((ObjectUtilities.GetMemberValue(segment,"time") - presentationTimeOffset) / (double) timescale);

            dynamic map = new ExpandoObject();
            map.uri = uri;
            map.timeline = ObjectUtilities.GetMemberValue(segment,"timeline");
            map.duration = ObjectUtilities.GetMemberValue(segment,"duration");
            map.resolvedUri = UrlUtils.ResolveUrl(ObjectUtilities.GetMemberValue(attributes,"baseUrl") ?? "", uri);
            map.map = mapSegment;
            map.number = ObjectUtilities.GetMemberValue(segment,"number");
            map.presentationTime = presentationTime;

            return map;
        }).ToList();
    }


    private static readonly Regex IdentifierPattern = new Regex(@"\$([A-Za-z]*)(?:(%0)([0-9]+)d)?\$", RegexOptions.Compiled);

    public static string ConstructTemplateUrl(string url, dynamic values){
        // Convert dynamic to IDictionary<string, object> for easier handling
        var valuesDictionary = (IDictionary<string, object>)values;
        return IdentifierPattern.Replace(url, match => IdentifierReplacement(match, valuesDictionary));
    }

    private static string IdentifierReplacement(Match match, IDictionary<string, object> values){
        if (match.Value == "$$"){
            // escape sequence
            return "$";
        }

        var identifier = match.Groups[1].Value;
        var format = match.Groups[2].Value;
        var widthStr = match.Groups[3].Value;

        if (!values.ContainsKey(identifier)){
            return match.Value;
        }

        var value = values[identifier]?.ToString() ?? "";

        if (identifier == "RepresentationID"){
            // Format tag shall not be present with RepresentationID
            return value;
        }

        int width = string.IsNullOrEmpty(format) ? 1 : int.Parse(widthStr);
        if (value.Length >= width){
            return value;
        }

        return value.PadLeft(width, '0');
    }

    public static List<dynamic> ParseTemplateInfo(dynamic attributes, List<dynamic> segmentTimeline){
        // Check if duration and SegmentTimeline are not present
        if (ObjectUtilities.GetMemberValue(attributes,"duration") == null && segmentTimeline == null){
            // Exactly one media segment expected
            return new List<dynamic>{
                new{
                    number = ObjectUtilities.GetMemberValue(attributes,"startNumber") ?? 1,
                    duration = ObjectUtilities.GetMemberValue(attributes,"sourceDuration"),
                    time = 0,
                    timeline = ObjectUtilities.GetMemberValue(attributes,"periodStart")
                }
            };
        }

        if (ObjectUtilities.GetMemberValue(attributes,"duration") != null){
            // Parse segments based on duration
            return DurationTimeParser.ParseByDuration(attributes);
        }

        // Parse segments based on SegmentTimeline
        return TimelineTimeParser.ParseByTimeline(attributes, segmentTimeline);
    }
}