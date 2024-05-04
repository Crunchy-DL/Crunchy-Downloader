using System;
using CRD.Utils.Parser.Utils;

namespace CRD.Utils.Parser.Segments;

public class UrlType{
    public static dynamic UrlTypeToSegment(dynamic input){
        dynamic segment = new {
            uri = ObjectUtilities.GetMemberValue(input,"source"),
            resolvedUri = new Uri(new Uri(input.baseUrl, UriKind.Absolute), input.source).ToString()
        };

        string rangeStr = !string.IsNullOrEmpty(input.range) ? ObjectUtilities.GetMemberValue(input,"range") : ObjectUtilities.GetMemberValue(input,"indexRange");
        if (!string.IsNullOrEmpty(rangeStr)){
            var ranges = rangeStr.Split('-');
            long startRange = long.Parse(ranges[0]);
            long endRange = long.Parse(ranges[1]);
            long length = endRange - startRange + 1;

            segment.ByteRange = new {
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