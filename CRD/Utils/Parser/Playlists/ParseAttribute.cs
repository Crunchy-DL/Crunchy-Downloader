using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Xml;
using CRD.Utils.Parser.Utils;

namespace CRD.Utils.Parser;

public class ParseAttribute{
    public static Dictionary<string, Func<string, object>> ParsersDictionary = new Dictionary<string, Func<string, object>>{
        { "mediaPresentationDuration", MediaPresentationDuration },
        { "availabilityStartTime", AvailabilityStartTime },
        { "minimumUpdatePeriod", MinimumUpdatePeriod },
        { "suggestedPresentationDelay", SuggestedPresentationDelay },
        { "type", Type },
        { "timeShiftBufferDepth", TimeShiftBufferDepth },
        { "start", Start },
        { "width", Width },
        { "height", Height },
        { "bandwidth", Bandwidth },
        { "audioSamplingRate", AudioSamplingRate },
        { "frameRate", FrameRate },
        { "startNumber", StartNumber },
        { "timescale", Timescale },
        { "presentationTimeOffset", PresentationTimeOffset },
        { "duration", Duration },
        { "d", D },
        { "t", T },
        { "r", R },
        { "presentationTime", PresentationTime },
        { "DEFAULT", DefaultParser }
    };

    public static object MediaPresentationDuration(string value) => DurationParser.ParseDuration(value);
    public static object AvailabilityStartTime(string value) => DurationParser.ParseDate(value) / 1000;
    public static object MinimumUpdatePeriod(string value) => DurationParser.ParseDuration(value);
    public static object SuggestedPresentationDelay(string value) => DurationParser.ParseDuration(value);
    public static object Type(string value) => value;
    public static object TimeShiftBufferDepth(string value) => DurationParser.ParseDuration(value);
    public static object Start(string value) => DurationParser.ParseDuration(value);
    public static object Width(string value) => int.Parse(value);
    public static object Height(string value) => int.Parse(value);
    public static object Bandwidth(string value) => int.Parse(value);
    public static object AudioSamplingRate(string value) => int.Parse(value);
    public static object FrameRate(string value) => DivisionValueParser.ParseDivisionValue(value);
    public static object StartNumber(string value) => int.Parse(value);
    public static object Timescale(string value) => int.Parse(value);
    public static object PresentationTimeOffset(string value) => int.Parse(value);

    public static object Duration(string value){
        if (int.TryParse(value, out int parsedValue)){
            return parsedValue;
        }

        return DurationParser.ParseDuration(value);
    }

    public static object D(string value) => int.Parse(value);
    public static object T(string value) => int.Parse(value);
    public static object R(string value) => int.Parse(value);
    public static object PresentationTime(string value) => int.Parse(value);
    public static object DefaultParser(string value) => value;

    // public static Dictionary<string, object> ParseAttributes(XmlNode el)
    // {
    //     if (!(el != null && el.Attributes != null))
    //     {
    //         return new Dictionary<string, object>();
    //     }
    //
    //     return el.Attributes.Cast<XmlAttribute>()
    //         .ToDictionary(attr => attr.Name, attr =>
    //         {
    //             Func<string, object> parseFn;
    //             if (ParsersDictionary.TryGetValue(attr.Name, out parseFn))
    //             {
    //                 return parseFn(attr.Value);
    //             }
    //             return DefaultParser(attr.Value);
    //         });
    // }

    public static dynamic ParseAttributes(XmlNode el){
        var expandoObj = new ExpandoObject() as IDictionary<string, object>;

        if (el != null && el.Attributes != null){
            foreach (XmlAttribute attr in el.Attributes){
                Func<string, object> parseFn;
                if (ParsersDictionary.TryGetValue(attr.Name, out parseFn)){
                    expandoObj[attr.Name] = parseFn(attr.Value);
                } else{
                    expandoObj[attr.Name] = DefaultParser(attr.Value);
                }
            }
        }

        return expandoObj;
    }
}

// public class ParsedAttributes{
//     public double MediaPresentationDuration{ get; set; }
//     public long AvailabilityStartTime{ get; set; }
//     public double MinimumUpdatePeriod{ get; set; }
//     public double SuggestedPresentationDelay{ get; set; }
//     public string Type{ get; set; }
//     public double TimeShiftBufferDepth{ get; set; }
//     public double? Start{ get; set; }
//     public int Width{ get; set; }
//     public int Height{ get; set; }
//     public int Bandwidth{ get; set; }
//     public double FrameRate{ get; set; }
//     public int StartNumber{ get; set; }
//     public int Timescale{ get; set; }
//     public int PresentationTimeOffset{ get; set; }
//     public double? Duration{ get; set; }
//     public int D{ get; set; }
//     public int T{ get; set; }
//     public int R{ get; set; }
//     public int PresentationTime{ get; set; }
//     
//     public int clientOffset{ get; set; }
//     
//     public long NOW{ get; set; }
//     public double sourceDuration{ get; set; }
//     public List<string> locations{ get; set; }
//     public string baseUrl{ get; set; }
//     public string? serviceLocation{ get; set; }
//
//     public ParsedAttributes(){
//         
//     }
//     
//     public ParsedAttributes(
//         double mediaPresentationDuration,
//         long availabilityStartTime,
//         double minimumUpdatePeriod,
//         double suggestedPresentationDelay,
//         string type,
//         double timeShiftBufferDepth,
//         double? start,
//         int width,
//         int height,
//         int bandwidth,
//         double frameRate,
//         int startNumber,
//         int timescale,
//         int presentationTimeOffset,
//         double? duration,
//         int d,
//         int t,
//         int r,
//         int presentationTime){
//         MediaPresentationDuration = mediaPresentationDuration;
//         AvailabilityStartTime = availabilityStartTime;
//         MinimumUpdatePeriod = minimumUpdatePeriod;
//         SuggestedPresentationDelay = suggestedPresentationDelay;
//         Type = type;
//         TimeShiftBufferDepth = timeShiftBufferDepth;
//         Start = start;
//         Width = width;
//         Height = height;
//         Bandwidth = bandwidth;
//         FrameRate = frameRate;
//         StartNumber = startNumber;
//         Timescale = timescale;
//         PresentationTimeOffset = presentationTimeOffset;
//         Duration = duration;
//         D = d;
//         T = t;
//         R = r;
//         PresentationTime = presentationTime;
//     }
// }
//
// public class ParseAttribute{
//     public static Dictionary<string, Func<string, object>> ParsersDictionary = new Dictionary<string, Func<string, object>>{
//         { "mediaPresentationDuration", MediaPresentationDuration },
//         { "availabilityStartTime", AvailabilityStartTime },
//         { "minimumUpdatePeriod", MinimumUpdatePeriod },
//         { "suggestedPresentationDelay", SuggestedPresentationDelay },
//         { "type", Type },
//         { "timeShiftBufferDepth", TimeShiftBufferDepth },
//         { "start", Start },
//         { "width", Width },
//         { "height", Height },
//         { "bandwidth", Bandwidth },
//         { "frameRate", FrameRate },
//         { "startNumber", StartNumber },
//         { "timescale", Timescale },
//         { "presentationTimeOffset", PresentationTimeOffset },
//         { "duration", Duration },
//         { "d", D },
//         { "t", T },
//         { "r", R },
//         { "presentationTime", PresentationTime },
//         { "DEFAULT", DefaultParser }
//     };
//
//     public static object MediaPresentationDuration(string value) => DurationParser.ParseDuration(value);
//     public static object AvailabilityStartTime(string value) => DurationParser.ParseDate(value) / 1000;
//     public static object MinimumUpdatePeriod(string value) => DurationParser.ParseDuration(value);
//     public static object SuggestedPresentationDelay(string value) => DurationParser.ParseDuration(value);
//     public static object Type(string value) => value;
//     public static object TimeShiftBufferDepth(string value) => DurationParser.ParseDuration(value);
//     public static object Start(string value) => DurationParser.ParseDuration(value);
//     public static object Width(string value) => int.Parse(value);
//     public static object Height(string value) => int.Parse(value);
//     public static object Bandwidth(string value) => int.Parse(value);
//     public static object FrameRate(string value) => DivisionValueParser.ParseDivisionValue(value);
//     public static object StartNumber(string value) => int.Parse(value);
//     public static object Timescale(string value) => int.Parse(value);
//     public static object PresentationTimeOffset(string value) => int.Parse(value);
//
//     public static object Duration(string value){
//         if (int.TryParse(value, out int parsedValue)){
//             return parsedValue;
//         }
//
//         return DurationParser.ParseDuration(value);
//     }
//
//     public static object D(string value) => int.Parse(value);
//     public static object T(string value) => int.Parse(value);
//     public static object R(string value) => int.Parse(value);
//     public static object PresentationTime(string value) => int.Parse(value);
//     public static object DefaultParser(string value) => value;
//
//     public static ParsedAttributes ParseAttributes(XmlNode el){
//         if (!(el != null && el.Attributes != null)){
//             return new ParsedAttributes(0, 0, 0, 0, "",  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
//         }
//         double mediaPresentationDuration = 0;
//         long availabilityStartTime = 0;
//         double minimumUpdatePeriod = 0;
//         double suggestedPresentationDelay = 0;
//         string type = "";
//         double timeShiftBufferDepth = 0;
//         double? start = null;
//         int width = 0;
//         int height = 0;
//         int bandwidth = 0;
//         double frameRate = 0;
//         int startNumber = 0;
//         int timescale = 0;
//         int presentationTimeOffset = 0;
//         double? duration = null;
//         int d = 0;
//         int t = 0;
//         int r = 0;
//         int presentationTime = 0;
//
//         foreach (XmlAttribute attr in el.Attributes){
//             Func<string, object> parseFn;
//             if (ParsersDictionary.TryGetValue(attr.Name, out parseFn)){
//                 switch (attr.Name){
//                     case "mediaPresentationDuration":
//                         mediaPresentationDuration = (double)parseFn(attr.Value);
//                         break;
//                     case "availabilityStartTime":
//                         availabilityStartTime = (long)parseFn(attr.Value);
//                         break;
//                     case "minimumUpdatePeriod":
//                         minimumUpdatePeriod = (double)parseFn(attr.Value);
//                         break;
//                     case "suggestedPresentationDelay":
//                         suggestedPresentationDelay = (double)parseFn(attr.Value);
//                         break;
//                     case "type":
//                         type = (string)parseFn(attr.Value);
//                         break;
//                     case "timeShiftBufferDepth":
//                         timeShiftBufferDepth = (double)parseFn(attr.Value);
//                         break;
//                     case "start":
//                         start = (double)parseFn(attr.Value);
//                         break;
//                     case "width":
//                         width = (int)parseFn(attr.Value);
//                         break;
//                     case "height":
//                         height = (int)parseFn(attr.Value);
//                         break;
//                     case "bandwidth":
//                         bandwidth = (int)parseFn(attr.Value);
//                         break;
//                     case "frameRate":
//                         frameRate = (double)parseFn(attr.Value);
//                         break;
//                     case "startNumber":
//                         startNumber = (int)parseFn(attr.Value);
//                         break;
//                     case "timescale":
//                         timescale = (int)parseFn(attr.Value);
//                         break;
//                     case "presentationTimeOffset":
//                         presentationTimeOffset = (int)parseFn(attr.Value);
//                         break;
//                     case "duration":
//                         duration = (double)parseFn(attr.Value);
//                         break;
//                     case "d":
//                         d = (int)parseFn(attr.Value);
//                         break;
//                     case "t":
//                         t = (int)parseFn(attr.Value);
//                         break;
//                     case "r":
//                         r = (int)parseFn(attr.Value);
//                         break;
//                     case "presentationTime":
//                         presentationTime = (int)parseFn(attr.Value);
//                         break;
//                     // Add cases for other attributes
//                 }
//             }
//         }
//
//         return new ParsedAttributes(mediaPresentationDuration, availabilityStartTime, minimumUpdatePeriod, suggestedPresentationDelay, type, timeShiftBufferDepth, start, width, height, bandwidth, frameRate, startNumber,
//             timescale, presentationTimeOffset, duration, d, t, r, presentationTime);
//     }
// }