using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using CRD.Views;

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

        public Dictionary<string, string> Fonts{ get; private set; } = new(){
        { "Adobe Arabic", "AdobeArabic-Bold.otf" },
        { "Andale Mono", "andalemo.ttf" },
        { "Arial", "arial.ttf" },
        { "Arial Black", "ariblk.ttf" },
        { "Arial Bold", "arialbd.ttf" },
        { "Arial Bold Italic", "arialbi.ttf" },
        { "Arial Italic", "ariali.ttf" },
        { "Arial Unicode MS", "arialuni.ttf" },
        { "Comic Sans MS", "comic.ttf" },
        { "Comic Sans MS Bold", "comicbd.ttf" },
        { "Courier New", "cour.ttf" },
        { "Courier New Bold", "courbd.ttf" },
        { "Courier New Bold Italic", "courbi.ttf" },
        { "Courier New Italic", "couri.ttf" },
        { "DejaVu LGC Sans Mono", "DejaVuLGCSansMono.ttf" },
        { "DejaVu LGC Sans Mono Bold", "DejaVuLGCSansMono-Bold.ttf" },
        { "DejaVu LGC Sans Mono Bold Oblique", "DejaVuLGCSansMono-BoldOblique.ttf" },
        { "DejaVu LGC Sans Mono Oblique", "DejaVuLGCSansMono-Oblique.ttf" },
        { "DejaVu Sans", "DejaVuSans.ttf" },
        { "DejaVu Sans Bold", "DejaVuSans-Bold.ttf" },
        { "DejaVu Sans Bold Oblique", "DejaVuSans-BoldOblique.ttf" },
        { "DejaVu Sans Condensed", "DejaVuSansCondensed.ttf" },
        { "DejaVu Sans Condensed Bold", "DejaVuSansCondensed-Bold.ttf" },
        { "DejaVu Sans Condensed Bold Oblique", "DejaVuSansCondensed-BoldOblique.ttf" },
        { "DejaVu Sans Condensed Oblique", "DejaVuSansCondensed-Oblique.ttf" },
        { "DejaVu Sans ExtraLight", "DejaVuSans-ExtraLight.ttf" },
        { "DejaVu Sans Mono", "DejaVuSansMono.ttf" },
        { "DejaVu Sans Mono Bold", "DejaVuSansMono-Bold.ttf" },
        { "DejaVu Sans Mono Bold Oblique", "DejaVuSansMono-BoldOblique.ttf" },
        { "DejaVu Sans Mono Oblique", "DejaVuSansMono-Oblique.ttf" },
        { "DejaVu Sans Oblique", "DejaVuSans-Oblique.ttf" },
        { "Gautami", "gautami.ttf" },
        { "Georgia", "georgia.ttf" },
        { "Georgia Bold", "georgiab.ttf" },
        { "Georgia Bold Italic", "georgiaz.ttf" },
        { "Georgia Italic", "georgiai.ttf" },
        { "Impact", "impact.ttf" },
        { "Mangal", "MANGAL.woff2" },
        { "Meera Inimai", "MeeraInimai-Regular.ttf" },
        { "Noto Sans Tamil", "NotoSansTamilVariable.ttf" },
        { "Noto Sans Telugu", "NotoSansTeluguVariable.ttf" },
        { "Noto Sans Thai", "NotoSansThai.ttf" },
        { "Rubik", "Rubik-Regular.ttf" },
        { "Rubik Black", "Rubik-Black.ttf" },
        { "Rubik Black Italic", "Rubik-BlackItalic.ttf" },
        { "Rubik Bold", "Rubik-Bold.ttf" },
        { "Rubik Bold Italic", "Rubik-BoldItalic.ttf" },
        { "Rubik Italic", "Rubik-Italic.ttf" },
        { "Rubik Light", "Rubik-Light.ttf" },
        { "Rubik Light Italic", "Rubik-LightItalic.ttf" },
        { "Rubik Medium", "Rubik-Medium.ttf" },
        { "Rubik Medium Italic", "Rubik-MediumItalic.ttf" },
        { "Tahoma", "tahoma.ttf" },
        { "Times New Roman", "times.ttf" },
        { "Times New Roman Bold", "timesbd.ttf" },
        { "Times New Roman Bold Italic", "timesbi.ttf" },
        { "Times New Roman Italic", "timesi.ttf" },
        { "Trebuchet MS", "trebuc.ttf" },
        { "Trebuchet MS Bold", "trebucbd.ttf" },
        { "Trebuchet MS Bold Italic", "trebucbi.ttf" },
        { "Trebuchet MS Italic", "trebucit.ttf" },
        { "Verdana", "verdana.ttf" },
        { "Verdana Bold", "verdanab.ttf" },
        { "Verdana Bold Italic", "verdanaz.ttf" },
        { "Verdana Italic", "verdanai.ttf" },
        { "Vrinda", "vrinda.ttf" },
        { "Vrinda Bold", "vrindab.ttf" },
        { "Webdings", "webdings.ttf" }
    };


    public string root = "https://static.crunchyroll.com/vilos-v2/web/vilos/assets/libass-fonts/";


    public async Task GetFontsAsync(){
        Console.WriteLine("Downloading fonts...");
        var fonts = Fonts.Values.ToList();

        foreach (var font in fonts){
            var fontLoc = Path.Combine(CfgManager.PathFONTS_DIR, font);

            if (File.Exists(fontLoc) && new FileInfo(fontLoc).Length != 0){
                // Console.WriteLine($"{font} already downloaded!");
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

                var httpClient = HttpClientReq.Instance.GetHttpClient();
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

    public Dictionary<string, string> GetDictFromKeyList(List<string> keysList){
        Dictionary<string, string> filteredDictionary = new Dictionary<string, string>();

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
        Dictionary<string, string> fontsNameList = new Dictionary<string, string>();
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

        List<string> missingFonts = new List<string>();

        foreach (var f in fontsNameList){
            if (Fonts.TryGetValue(f.Key, out var fontFile)){
                string fontPath = Path.Combine(fontsDir, fontFile);
                string mime = GetFontMimeType(fontFile);
                if (File.Exists(fontPath) && new FileInfo(fontPath).Length != 0){
                    fontsList.Add(new ParsedFont{ Name = fontFile, Path = fontPath, Mime = mime });
                }
            } else{
                missingFonts.Add(f.Key);
            }
        }

        if (missingFonts.Count > 0){
            MainWindow.Instance.ShowError($"Missing Fonts: \n{string.Join(", ", fontsNameList)}");
        }

        return fontsList;
    }
}

public class SubtitleFonts{
    public LanguageItem Language{ get; set; }
    public Dictionary<string, string> Fonts{ get; set; }
}