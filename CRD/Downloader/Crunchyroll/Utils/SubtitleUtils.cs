using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CRD.Utils;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll;

namespace CRD.Downloader.Crunchyroll.Utils;

public static class SubtitleUtils{
    private static readonly Dictionary<string, string> StyleTemplates = new(){
        { "de-DE", "Style: {name},Arial,23,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,{align},0000,0000,0020,1" },
        { "ar-SA", "Style: {name},Adobe Arabic,26,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,-1,0,0,0,100,100,0,0,1,1,0,{align},0010,0010,0018,0" },
        { "en-US", "Style: {name},Trebuchet MS,24,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "es-419", "Style: {name},Trebuchet MS,24,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,1" },
        { "es-ES", "Style: {name},Trebuchet MS,24,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,1" },
        { "fr-FR", "Style: {name},Trebuchet MS,22,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,-1,0,0,0,100,100,0,0,1,1,1,{align},0002,0002,0025,1" },
        { "id-ID", "Style: {name},Arial,20,&H00FFFFFF,&H0000FFFF,&H00000000,&H7F404040,-1,0,0,0,100,100,0,0,1,2,1,{align},0020,0020,0022,0" },
        { "it-IT", "Style: {name},Trebuchet MS,22,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,{align},0010,0010,0015,1" },
        { "ms-MY", "Style: {name},Arial,20,&H00FFFFFF,&H0000FFFF,&H00000000,&H7F404040,-1,0,0,0,100,100,0,0,1,2,1,{align},0020,0020,0022,0" },
        { "pt-BR", "Style: {name},Trebuchet MS,22,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,-1,0,0,0,100,100,0,0,1,2,1,{align},0040,0040,0015,0" },
        { "ru-RU", "Style: {name},Tahoma,22,&H00FFFFFF,&H000000FF,&H00000000,&H96000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0025,204" },
        { "th-TH", "Style: {name},Noto Sans Thai,30,&H00FFFFFF,&H0000FFFF,&H00000000,&H7F404040,-1,0,0,0,100,100,0,0,1,2,1,{align},0020,0020,0022,0" },
        { "vi-VN", "Style: {name},Arial Unicode MS,20,&H00FFFFFF,&H0000FFFF,&H00000000,&H7F404040,-1,0,0,0,100,100,0,0,1,2,1,{align},0020,0020,0022,0" },
        { "zh-CN", "Style: {name},Arial Unicode MS,20,&H00FFFFFF,&H0000FFFF,&H00000000,&H7F404040,-1,0,0,0,100,100,0,0,1,2,1,{align},0020,0020,0022,0" },
        { "zh-HK", "Style: {name},Arial Unicode MS,20,&H00FFFFFF,&H0000FFFF,&H00000000,&H7F404040,-1,0,0,0,100,100,0,0,1,2,1,{align},0020,0020,0022,0" },

        // Need to check
        { "ja-JP", "Style: {name},Arial Unicode MS,23,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "en-IN", "Style: {name},Trebuchet MS,24,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "pt-PT", "Style: {name},Trebuchet MS,22,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "pl-PL", "Style: {name},Trebuchet MS,22,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "ca-ES", "Style: {name},Trebuchet MS,22,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "tr-TR", "Style: {name},Trebuchet MS,22,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "hi-IN", "Style: {name},Noto Sans Devanagari,26,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "ta-IN", "Style: {name},Noto Sans Tamil,26,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "te-IN", "Style: {name},Noto Sans Telugu,26,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
        { "zh-TW", "Style: {name},Arial Unicode MS,20,&H00FFFFFF,&H0000FFFF,&H00000000,&H7F404040,-1,0,0,0,100,100,0,0,1,2,1,{align},0020,0020,0022,0" },
        { "ko-KR", "Style: {name},Malgun Gothic,22,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,1,{align},0010,0010,0018,0" },
    };


    public static string CleanAssAndEnsureScriptInfo(string assText, CrDownloadOptions options, LanguageItem langItem){
        if (string.IsNullOrEmpty(assText))
            return assText;

        string? scaledLine = options.SubsAddScaledBorder switch{
            ScaledBorderAndShadowSelection.ScaledBorderAndShadowYes => "ScaledBorderAndShadow: yes",
            ScaledBorderAndShadowSelection.ScaledBorderAndShadowNo => "ScaledBorderAndShadow: no",
            _ => null
        };

        bool isCcc = assText.Contains("www.closedcaptionconverter.com", StringComparison.OrdinalIgnoreCase);
        
        if (isCcc && options.FixCccSubtitles){
            assText = Regex.Replace(
                assText,
                @"^[ \t]*;[ \t]*Script generated by Closed Caption Converter \| www\.closedcaptionconverter\.com[ \t]*\r?\n",
                "",
                RegexOptions.Multiline
            );

            assText = Regex.Replace(
                assText,
                @"^[ \t]*PlayDepth[ \t]*:[ \t]*0[ \t]*\r?\n?",
                "",
                RegexOptions.Multiline | RegexOptions.IgnoreCase
            );

            assText = assText.Replace(",,,,25.00,,", ",,0,0,0,,");

            assText = FixStyles(assText, langItem.CrLocale);
        }
        
        // Remove Aegisub garbage and other useless metadata
        assText = RemoveAegisubProjectGarbageBlocks(assText);

        // Remove Aegisub-generated comments and YCbCr Matrix lines
        assText = Regex.Replace(
            assText,
            @"^[ \t]*;[^\r\n]*\r?\n?", // all comment lines starting with ';'
            "",
            RegexOptions.Multiline
        );

        assText = Regex.Replace(
            assText,
            @"^[ \t]*YCbCr Matrix:[^\r\n]*\r?\n?",
            "",
            RegexOptions.Multiline | RegexOptions.IgnoreCase
        );

        // Remove empty lines (but keep one between sections)
        assText = Regex.Replace(assText, @"(\r?\n){3,}", "\r\n\r\n");

        var linesToEnsure = new Dictionary<string, string>();

        if (isCcc){
            linesToEnsure["PlayResX"] = "PlayResX: 640";
            linesToEnsure["PlayResY"] = "PlayResY: 360";
            linesToEnsure["Timer"] = "Timer: 0.0000";
            linesToEnsure["WrapStyle"] = "WrapStyle: 0";
        }

        if (scaledLine != null)
            linesToEnsure["ScaledBorderAndShadow"] = scaledLine;

        if (linesToEnsure.Count > 0)
            assText = UpsertScriptInfo(assText, linesToEnsure);

        return assText;
    }

    private static string UpsertScriptInfo(string input, IDictionary<string, string> linesToEnsure){
        var rxSection = new Regex(@"(?is)(\[Script Info\]\s*\r?\n)(.*?)(?=\r?\n\[|$)");
        var m = rxSection.Match(input);

        string nl = input.Contains("\r\n") ? "\r\n" : "\n";

        if (!m.Success){
            // Create whole section at top
            return "[Script Info]" + nl
                                   + string.Join(nl, linesToEnsure.Values) + nl
                                   + input;
        }

        string header = m.Groups[1].Value;
        string body = m.Groups[2].Value;
        string bodyNl = header.Contains("\r\n") ? "\r\n" : "\n";

        foreach (var kv in linesToEnsure){
            var lineRx = new Regex($@"(?im)^\s*{Regex.Escape(kv.Key)}\s*:\s*.*$");
            if (lineRx.IsMatch(body))
                body = lineRx.Replace(body, kv.Value);
            else
                body = body.TrimEnd() + bodyNl + kv.Value + bodyNl;
        }

        return input.Substring(0, m.Index) + header + body + input.Substring(m.Index + m.Length);
    }

    private static string FixStyles(string assContent, string crLocale){
        var pattern = @"^Style:\s*([^,]+),\s*(?:[^,\r\n]*,\s*){17}(\d+)\s*,[^\r\n]*$";

        string template = StyleTemplates.TryGetValue(crLocale, out var tmpl) ? tmpl : StyleTemplates["en-US"];

        return Regex.Replace(assContent, pattern, m => {
            string name = m.Groups[1].Value;
            string align = m.Groups[2].Value;

            return template
                .Replace("{name}", name)
                .Replace("{align}", align);
        }, RegexOptions.Multiline);
    }
    
    private static string RemoveAegisubProjectGarbageBlocks(string text){
        if (string.IsNullOrEmpty(text)) return text;
        
        var nl = "\n";
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        var sb = new System.Text.StringBuilder(text.Length);
        using var sr = new System.IO.StringReader(text);

        bool skipping = false;
        string? line;
        while ((line = sr.ReadLine()) != null){
            string trimmed = line.Trim();
            
            if (!skipping && Regex.IsMatch(trimmed, @"^\[\s*Aegisub\s+Project\s+Garbage\s*\]$", RegexOptions.IgnoreCase)){
                skipping = true;
                continue;
            }

            if (skipping){
                if (trimmed.Length == 0 || Regex.IsMatch(trimmed, @"^\[.+\]$")){
                    skipping = false;
                    
                    if (trimmed.Length != 0){
                        sb.Append(line).Append(nl);
                    }
                }
                
                continue;
            }
            
            sb.Append(line).Append(nl);
        }
        
        return sb.ToString().TrimEnd('\n').Replace("\n", "\r\n");
    }
}