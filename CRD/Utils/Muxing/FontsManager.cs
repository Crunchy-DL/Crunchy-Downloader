using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CRD.Utils.Files;
using CRD.Utils.Structs;

namespace CRD.Utils.Muxing;

public class FontsManager{
    #region Singelton

    private static FontsManager? instance;
    private static readonly object padlock = new object();

    public static FontsManager Instance{
        get{
            if (instance == null){
                lock (padlock){
                    if (instance == null){
                        instance = new FontsManager();
                    }
                }
            }

            return instance;
        }
    }

    #endregion

    public Dictionary<string, List<string>> Fonts{ get; private set; } = new(){
        { "Adobe Arabic", new List<string>{ "AdobeArabic-Bold.otf" } },
        { "Andale Mono", new List<string>{ "andalemo.ttf" } },
        { "Arial", new List<string>{ "arial.ttf", "arialbd.ttf", "arialbi.ttf", "ariali.ttf" } },
        { "Arial Unicode MS", new List<string>{ "arialuni.ttf" } },
        { "Arial Black", new List<string>{ "ariblk.ttf" } },
        { "Comic Sans MS", new List<string>{ "comic.ttf", "comicbd.ttf" } },
        { "Courier New", new List<string>{ "cour.ttf", "courbd.ttf", "courbi.ttf", "couri.ttf" } },
        { "DejaVu LGC Sans Mono", new List<string>{ "DejaVuLGCSansMono-Bold.ttf", "DejaVuLGCSansMono-BoldOblique.ttf", "DejaVuLGCSansMono-Oblique.ttf", "DejaVuLGCSansMono.ttf" } },
        { "DejaVu Sans", new List<string>{ "DejaVuSans-Bold.ttf", "DejaVuSans-BoldOblique.ttf", "DejaVuSans-ExtraLight.ttf", "DejaVuSans-Oblique.ttf", "DejaVuSans.ttf" } },
        { "DejaVu Sans Condensed", new List<string>{ "DejaVuSansCondensed-Bold.ttf", "DejaVuSansCondensed-BoldOblique.ttf", "DejaVuSansCondensed-Oblique.ttf", "DejaVuSansCondensed.ttf" } },
        { "DejaVu Sans Mono", new List<string>{ "DejaVuSansMono-Bold.ttf", "DejaVuSansMono-BoldOblique.ttf", "DejaVuSansMono-Oblique.ttf", "DejaVuSansMono.ttf" } },
        { "Georgia", new List<string>{ "georgia.ttf", "georgiab.ttf", "georgiai.ttf", "georgiaz.ttf" } },
        { "Impact", new List<string>{ "impact.ttf" } },
        { "Rubik Black", new List<string>{ "Rubik-Black.ttf", "Rubik-BlackItalic.ttf" } },
        { "Rubik", new List<string>{ "Rubik-Bold.ttf", "Rubik-BoldItalic.ttf", "Rubik-Italic.ttf", "Rubik-Light.ttf", "Rubik-LightItalic.ttf", "Rubik-Medium.ttf", "Rubik-MediumItalic.ttf", "Rubik-Regular.ttf" } },
        { "Tahoma", new List<string>{ "tahoma.ttf" } },
        { "Times New Roman", new List<string>{ "times.ttf", "timesbd.ttf", "timesbi.ttf", "timesi.ttf" } },
        { "Trebuchet MS", new List<string>{ "trebuc.ttf", "trebucbd.ttf", "trebucbi.ttf", "trebucit.ttf" } },
        { "Verdana", new List<string>{ "verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf" } },
        { "Webdings", new List<string>{ "webdings.ttf" } },
    };

    public string root = "https://static.crunchyroll.com/vilos-v2/web/vilos/assets/libass-fonts/";


    public async Task GetFontsAsync(){
        Console.WriteLine("Downloading fonts...");
        var fonts = Fonts.Values.SelectMany(f => f).ToList();

        foreach (var font in fonts){
            var fontLoc = Path.Combine(CfgManager.PathFONTS_DIR, font);

            if (File.Exists(fontLoc) && new FileInfo(fontLoc).Length != 0){
                Console.WriteLine($"{font} already downloaded!");
            } else{
                var fontFolder = Path.GetDirectoryName(fontLoc);
                if (File.Exists(fontLoc) && new FileInfo(fontLoc).Length == 0){
                    File.Delete(fontLoc);
                }

                try{
                    if (!Directory.Exists(fontFolder)){
                        Directory.CreateDirectory(fontFolder);
                    }
                } catch (Exception e){
                    Console.WriteLine($"Failed to create directory: {e.Message}");
                }

                var fontUrl = root + font;

                using (var httpClient = HttpClientReq.Instance.GetHttpClient()){
                    try{
                        var response = await httpClient.GetAsync(fontUrl);
                        if (response.IsSuccessStatusCode){
                            var fontData = await response.Content.ReadAsByteArrayAsync();
                            await File.WriteAllBytesAsync(fontLoc, fontData);
                            Console.WriteLine($"Downloaded: {font}");
                        } else{
                            Console.Error.WriteLine($"Failed to download: {font}");
                        }
                    } catch (Exception e){
                        Console.Error.WriteLine($"Error downloading {font}: {e.Message}");
                    }
                }
            }
        }

        Console.WriteLine("All required fonts downloaded!");
    }


    public static List<string> ExtractFontsFromAss(string ass){
        var lines = ass.Replace("\r", "").Split('\n');
        var styles = new List<string>();

        foreach (var line in lines){
            if (line.StartsWith("Style: ")){
                var parts = line.Split(',');
                if (parts.Length > 1)
                    styles.Add(parts[1].Trim());
            }
        }

        var fontMatches = Regex.Matches(ass, @"\\fn([^\\}]+)");
        foreach (Match match in fontMatches){
            if (match.Groups.Count > 1)
                styles.Add(match.Groups[1].Value);
        }

        return styles.Distinct().ToList(); // Using Linq to remove duplicates
    }

    public Dictionary<string, List<string>> GetDictFromKeyList(List<string> keysList){
        Dictionary<string, List<string>> filteredDictionary = new Dictionary<string, List<string>>();

        foreach (string key in keysList){
            if (Fonts.TryGetValue(key, out var font)){
                filteredDictionary.Add(key, font);
            }
        }

        return filteredDictionary;
    }


    public static string GetFontMimeType(string fontFile){
        if (Regex.IsMatch(fontFile, @"\.otf$"))
            return "application/vnd.ms-opentype";
        else if (Regex.IsMatch(fontFile, @"\.ttf$"))
            return "application/x-truetype-font";
        else
            return "application/octet-stream";
    }

    public List<ParsedFont> MakeFontsList(string fontsDir, List<SubtitleFonts> subs){
        Dictionary<string, List<string>> fontsNameList = new Dictionary<string, List<string>>();
        List<string> subsList = new List<string>();
        List<ParsedFont> fontsList = new List<ParsedFont>();
        bool isNstr = true;

        foreach (var s in subs){
            foreach (var keyValuePair in s.Fonts){
                if (!fontsNameList.ContainsKey(keyValuePair.Key)){
                    fontsNameList.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }

            subsList.Add(s.Language.Locale);
        }

        if (subsList.Count > 0){
            Console.WriteLine("\nSubtitles: {0} (Total: {1})", string.Join(", ", subsList), subsList.Count);
            isNstr = false;
        }

        if (fontsNameList.Count > 0){
            Console.WriteLine((isNstr ? "\n" : "") + "Required fonts: {0} (Total: {1})", string.Join(", ", fontsNameList), fontsNameList.Count);
        }

        foreach (var f in fontsNameList){
            if (Fonts.TryGetValue(f.Key, out var fontFiles)){
                foreach (var fontFile in fontFiles){
                    string fontPath = Path.Combine(fontsDir, fontFile);
                    string mime = GetFontMimeType(fontFile);
                    if (File.Exists(fontPath) && new FileInfo(fontPath).Length != 0){
                        fontsList.Add(new ParsedFont{ Name = fontFile, Path = fontPath, Mime = mime });
                    }
                }
            }
        }

        return fontsList;
    }
}

public class SubtitleFonts{
    public LanguageItem Language{ get; set; }
    public Dictionary<string, List<string>> Fonts{ get; set; }
}