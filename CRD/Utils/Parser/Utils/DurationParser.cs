using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CRD.Utils.Parser.Utils;

public class DurationParser{
    private const int SECONDS_IN_YEAR = 365 * 24 * 60 * 60;
    private const int SECONDS_IN_MONTH = 30 * 24 * 60 * 60;
    private const int SECONDS_IN_DAY = 24 * 60 * 60;
    private const int SECONDS_IN_HOUR = 60 * 60;
    private const int SECONDS_IN_MIN = 60;

    public static double ParseDuration(string str){
        // P10Y10M10DT10H10M10.1S
        Regex durationRegex = new Regex(@"P(?:(\d*)Y)?(?:(\d*)M)?(?:(\d*)D)?(?:T(?:(\d*)H)?(?:(\d*)M)?(?:([\d.]*)S)?)?");
        Match match = durationRegex.Match(str);

        if (!match.Success){
            return 0;
        }

        double year = string.IsNullOrEmpty(match.Groups[1].Value) ? 0 : GetDouble(match.Groups[1].Value,0);
        double month = string.IsNullOrEmpty(match.Groups[2].Value) ? 0 : GetDouble(match.Groups[2].Value,0);
        double day = string.IsNullOrEmpty(match.Groups[3].Value) ? 0 : GetDouble(match.Groups[3].Value,0);
        double hour = string.IsNullOrEmpty(match.Groups[4].Value) ? 0 : GetDouble(match.Groups[4].Value,0);
        double minute = string.IsNullOrEmpty(match.Groups[5].Value) ? 0 : GetDouble(match.Groups[5].Value,0);
        double second = string.IsNullOrEmpty(match.Groups[6].Value) ? 0 : GetDouble(match.Groups[6].Value,0);

        return (year * SECONDS_IN_YEAR +
                month * SECONDS_IN_MONTH +
                day * SECONDS_IN_DAY +
                hour * SECONDS_IN_HOUR +
                minute * SECONDS_IN_MIN +
                second);
    }

    public static double GetDouble(string value, double defaultValue){
        double result;

        // // Try parsing in the current culture
        // if (!double.TryParse(value, System.Globalization.NumberStyles.Any, CultureInfo.CurrentCulture, out result) &&
        //     // Then try in US english
        //     !double.TryParse(value, System.Globalization.NumberStyles.Any, CultureInfo.GetCultureInfo("en-US"), out result) &&
        //     // Then in neutral language
        //     !double.TryParse(value, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out result))
        // {
        //     result = defaultValue;
        // }
        return double.Parse(value, CultureInfo.InvariantCulture);
    }
    
    public static long ParseDate(string str){
        // Date format without timezone according to ISO 8601
        // YYY-MM-DDThh:mm:ss.ssssss
        string dateRegexPattern = @"^\d+-\d+-\d+T\d+:\d+:\d+(\.\d+)?$";

        // If the date string does not specify a timezone, we must specify UTC. This is
        // expressed by ending with 'Z'
        if (Regex.IsMatch(str, dateRegexPattern)){
            str += 'Z';
        }

        return DateTimeOffset.Parse(str).ToUnixTimeMilliseconds();
    }
}