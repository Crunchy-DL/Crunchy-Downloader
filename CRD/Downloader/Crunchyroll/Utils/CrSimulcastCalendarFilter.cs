using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CRD.Downloader.Crunchyroll.Utils;

public class CrSimulcastCalendarFilter{
    private static readonly Regex SeasonLangSuffix =
        new Regex(@"\bSeason\s+\d+\s*\((?<tag>.*)\)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] NonLanguageTags ={
        "uncut", "simulcast", "sub", "subbed"
    };

    private static readonly string[] LanguageHints ={
        "deutsch", "german",
        "español", "espanol", "spanish", "américa latina", "america latina", "latin america",
        "português", "portugues", "portuguese", "brasil", "brazil",
        "français", "francais", "french",
        "italiano", "italian",
        "english",
        "рус", "russian",
        "한국", "korean",
        "中文", "普通话", "mandarin",
        "ไทย", "thai",
        "türk", "turk", "turkish",
        "polski", "polish",
        "nederlands", "dutch"
    };

    public static bool IsDubOrAltLanguageSeason(string? seasonName){
        if (string.IsNullOrWhiteSpace(seasonName))
            return false;

        // Explicit "Dub" anywhere
        if (seasonName.Contains("dub", StringComparison.OrdinalIgnoreCase))
            return true;

        // "Season N ( ... )" suffix
        var m = SeasonLangSuffix.Match(seasonName);
        if (!m.Success)
            return false;

        var tag = m.Groups["tag"].Value.Trim();
        if (tag.Length == 0)
            return false;

        foreach (var nl in NonLanguageTags)
            if (tag.Contains(nl, StringComparison.OrdinalIgnoreCase))
                return false;

        // Non-ASCII in the tag (e.g., 中文, Español, Português)
        if (tag.Any(c => c > 127))
            return true;

        // Otherwise look for known language hints
        foreach (var hint in LanguageHints)
            if (tag.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    #region Name Match to upcoming

    private static readonly Regex TrailingParenGroups =
        new Regex(@"\s*(\([^)]*\))\s*$", RegexOptions.Compiled);

    public static bool IsMatch(string? a, string? b, double similarityThreshold = 0.85){
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        var na = Normalize(a);
        var nb = Normalize(b);

        if (string.Equals(na, nb, StringComparison.OrdinalIgnoreCase))
            return true;

        if (na.Length >= 8 && nb.Length >= 8 &&
            (na.Contains(nb, StringComparison.OrdinalIgnoreCase) ||
             nb.Contains(na, StringComparison.OrdinalIgnoreCase)))
            return true;

        return Similarity(na, nb) >= similarityThreshold;
    }

    private static string Normalize(string s){
        s = s.Trim();

        while (TrailingParenGroups.IsMatch(s))
            s = TrailingParenGroups.Replace(s, "").TrimEnd();

        s = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s){
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        s = sb.ToString().Normalize(NormalizationForm.FormC);

        var cleaned = new StringBuilder(s.Length);
        foreach (var ch in s)
            cleaned.Append(char.IsLetterOrDigit(ch) ? ch : ' ');

        return Regex.Replace(cleaned.ToString(), @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static double Similarity(string a, string b){
        if (a.Length == 0 && b.Length == 0) return 1.0;
        int dist = LevenshteinDistance(a, b);
        int maxLen = Math.Max(a.Length, b.Length);
        return 1.0 - (double)dist / maxLen;
    }

    private static int LevenshteinDistance(string a, string b){
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (int i = 1; i <= a.Length; i++){
            curr[0] = i;
            for (int j = 1; j <= b.Length; j++){
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost
                );
            }

            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }

    #endregion
}