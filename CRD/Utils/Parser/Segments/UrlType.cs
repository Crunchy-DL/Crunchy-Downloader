using System;
using System.Dynamic;
using CRD.Utils.Parser.Utils;

namespace CRD.Utils.Parser.Segments;

public class UrlType{
    public static dynamic UrlTypeToSegment(dynamic input){
        string baseUrl = Convert.ToString(ObjectUtilities.GetMemberValue(input, "baseUrl"));
        string source = Convert.ToString(ObjectUtilities.GetMemberValue(input, "source"));

        var baseUri = new Uri(baseUrl, UriKind.Absolute);

        dynamic segment = new ExpandoObject();
        segment.uri = source;
        segment.resolvedUri = new Uri(baseUri, source).ToString();

        string rangeStr = Convert.ToString(
            !string.IsNullOrEmpty(Convert.ToString(ObjectUtilities.GetMemberValue(input, "range")))
                ? ObjectUtilities.GetMemberValue(input, "range")
                : ObjectUtilities.GetMemberValue(input, "indexRange")
        );

        if (!string.IsNullOrEmpty(rangeStr)){
            var ranges = rangeStr.Split('-');

            long startRange = long.Parse(ranges[0]);
            long endRange = long.Parse(ranges[1]);
            long length = endRange - startRange + 1;

            segment.ByteRange = new{
                length = length,
                offset = startRange
            };
        }

        return segment;
    }


    public static string ByteRangeToString(dynamic byteRange){
        long endRange = byteRange.offset + byteRange.length - 1;
        return $"{byteRange.offset}-{endRange}";
    }
}