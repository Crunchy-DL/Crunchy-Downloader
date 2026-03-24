using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CRD.Utils.Files;
using CRD.Utils.Muxing.Structs;
using CRD.Utils.Structs;
using CRD.Views;
using SixLabors.Fonts;

namespace CRD.Utils.Muxing.Fonts;

public class FontsManager{
    #region Singelton

    private static readonly Lock Padlock = new Lock();

    public static FontsManager Instance{
        get{
            if (field == null){
                lock (Padlock){
                    if (field == null){
                        field = new FontsManager();
                    }
                }
            }

            return field;
        }
    }

    #endregion

    public Dictionary<string, string> Fonts{ get; private set; } = new(StringComparer.OrdinalIgnoreCase){
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

    private string root = "https://static.crunchyroll.com/vilos-v2/web/vilos/assets/libass-fonts/";


    private readonly FontIndex index = new();

    private void EnsureIndex(string fontsDir){
        index.Rebuild(fontsDir);
    }

    public async Task GetFontsAsync(){
        Console.WriteLine("Downloading fonts...");
        var fonts = Fonts.Values.ToList();

        foreach (var font in fonts){
            var fontLoc = Path.Combine(CfgManager.PathFONTS_DIR, font);

            if (File.Exists(fontLoc) && new FileInfo(fontLoc).Length != 0){
                continue;
            }

            var fontFolder = Path.GetDirectoryName(fontLoc);
            if (File.Exists(fontLoc) && new FileInfo(fontLoc).Length == 0)
                File.Delete(fontLoc);

            try{
                if (!Directory.Exists(fontFolder))
                    Directory.CreateDirectory(fontFolder!);
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

        Console.WriteLine("All required fonts downloaded!");
    }

    public static List<string> ExtractFontsFromAss(string ass, bool checkTypesettingFonts){
        if (string.IsNullOrWhiteSpace(ass))
            return new List<string>();

        ass = ass.Replace("\r", "");
        var lines = ass.Split('\n');

        var fonts = new List<string>();

        foreach (var line in lines){
            if (line.StartsWith("Style: ", StringComparison.OrdinalIgnoreCase)){
                var parts = line.Substring(7).Split(',');
                if (parts.Length > 1){
                    var fontName = parts[1].Trim();
                    fonts.Add(NormalizeFontKey(fontName));
                }
            }
        }

        if (checkTypesettingFonts){
            var fontMatches = Regex.Matches(ass, @"\\fn([^\\}]+)");
            foreach (Match match in fontMatches){
                if (match.Groups.Count > 1){
                    var fontName = match.Groups[1].Value.Trim();

                    if (Regex.IsMatch(fontName, @"^\d+$"))
                        continue;

                    fonts.Add(NormalizeFontKey(fontName));
                }
            }
        }

        return fonts
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }


    public Dictionary<string, string> GetDictFromKeyList(List<string> keysList, bool keepUnknown = true){
        Dictionary<string, string> filteredDictionary = new(StringComparer.OrdinalIgnoreCase);

        foreach (string key in keysList){
            var k = NormalizeFontKey(key);

            if (Fonts.TryGetValue(k, out var fontFile)){
                filteredDictionary[k] = fontFile;
            } else if (keepUnknown){
                filteredDictionary[k] = k;
            }
        }

        return filteredDictionary;
    }

    public static string GetFontMimeType(string fontFileOrPath){
        var ext = Path.GetExtension(fontFileOrPath);
        if (ext.Equals(".otf", StringComparison.OrdinalIgnoreCase))
            return "application/vnd.ms-opentype";
        if (ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase))
            return "application/x-truetype-font";
        if (ext.Equals(".ttc", StringComparison.OrdinalIgnoreCase) || ext.Equals(".otc", StringComparison.OrdinalIgnoreCase))
            return "application/x-truetype-font";
        if (ext.Equals(".woff", StringComparison.OrdinalIgnoreCase))
            return "font/woff";
        if (ext.Equals(".woff2", StringComparison.OrdinalIgnoreCase))
            return "font/woff2";
        return "application/octet-stream";
    }

    public List<ParsedFont> MakeFontsList(string fontsDir, List<SubtitleFonts> subs){
        EnsureIndex(fontsDir);

        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var subsLocales = new List<string>();
        var fontsList = new List<ParsedFont>();
        var missing = new List<string>();

        foreach (var s in subs){
            subsLocales.Add(s.Language.Locale);

            foreach (var kv in s.Fonts)
                required.Add(NormalizeFontKey(kv.Key));
        }

        if (subsLocales.Count > 0)
            Console.WriteLine("\nSubtitles: {0} (Total: {1})", string.Join(", ", subsLocales), subsLocales.Count);

        if (required.Count > 0)
            Console.WriteLine("Required fonts: {0} (Total: {1})", string.Join(", ", required), required.Count);

        foreach (var requested in required){
            if (TryResolveFontPath(requested, fontsDir, out var resolvedPath, out var exact)){
                if (!File.Exists(resolvedPath) || new FileInfo(resolvedPath).Length == 0){
                    missing.Add(requested);
                    continue;
                }

                var attachName = MakeUniqueAttachmentName(resolvedPath, fontsList);

                fontsList.Add(new ParsedFont{
                    Name = attachName,
                    Path = resolvedPath,
                    Mime = GetFontMimeType(resolvedPath)
                });

                if (!exact) Console.WriteLine($"Soft-resolved '{requested}' -> '{Path.GetFileName(resolvedPath)}'");
            } else{
                missing.Add(requested);
            }
        }

        if (missing.Count > 0)
            MainWindow.Instance.ShowError($"Missing Fonts:\n{string.Join(", ", missing)}");

        return fontsList;
    }

    private bool TryResolveFontPath(string requestedName, string fontsDir, out string resolvedPath, out bool isExactMatch){
        resolvedPath = string.Empty;
        isExactMatch = true;

        var req = NormalizeFontKey(requestedName);

        if (index.TryResolve(req, out resolvedPath))
            return true;

        if (Fonts.TryGetValue(req, out var crFile)){
            var p = Path.Combine(fontsDir, crFile);
            if (File.Exists(p)){
                resolvedPath = p;
                return true;
            }
        }

        var family = StripStyleSuffix(req);
        if (!family.Equals(req, StringComparison.OrdinalIgnoreCase)){
            isExactMatch = false;

            if (index.TryResolve(family, out resolvedPath))
                return true;

            if (Fonts.TryGetValue(family, out var crFamilyFile)){
                var p = Path.Combine(fontsDir, crFamilyFile);
                if (File.Exists(p)){
                    resolvedPath = p;
                    return true;
                }
            }
        }

        return false;
    }

    private static string StripStyleSuffix(string name){
        var n = name;

        n = Regex.Replace(n, @"\s+(Bold\s+Italic|Bold\s+Oblique|Black\s+Italic|Black|Bold|Italic|Oblique|Regular)$",
            "", RegexOptions.IgnoreCase).Trim();

        return n;
    }

    public static string NormalizeFontKey(string s){
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        s = s.Trim().Trim('"');

        if (s.StartsWith("@"))
            s = s.Substring(1);

        s = Regex.Replace(s, @"(?<=[a-z])([A-Z])", " $1");

        s = s.Replace('_', ' ').Replace('-', ' ');

        s = Regex.Replace(s, @"\s+", " ").Trim();

        s = Regex.Replace(s, @"\s+Regular$", "", RegexOptions.IgnoreCase);

        return s;
    }

    private static string MakeUniqueAttachmentName(string path, List<ParsedFont> existing){
        var baseName = Path.GetFileName(path);

        if (existing.All(e => !baseName.Equals(e.Name, StringComparison.OrdinalIgnoreCase)))
            return baseName;

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(path)))
            .Substring(0, 8)
            .ToLowerInvariant();

        return $"{hash}-{baseName}";
    }


    private sealed class FontIndex{
        private readonly Dictionary<string, Candidate> map = new(StringComparer.OrdinalIgnoreCase);

        public void Rebuild(string fontsDir){
            map.Clear();
            if (!Directory.Exists(fontsDir)) return;

            foreach (var path in Directory.EnumerateFiles(fontsDir, "*.*", SearchOption.AllDirectories)){
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext is not (".ttf" or ".otf" or ".ttc" or ".otc" or ".woff" or ".woff2"))
                    continue;

                foreach (var desc in LoadDescriptions(path)){
                    foreach (var alias in BuildAliases(desc)){
                        Add(alias, path);
                    }
                }
            }
        }

        public bool TryResolve(string fontName, out string path){
            path = string.Empty;
            if (string.IsNullOrWhiteSpace(fontName)) return false;

            var key = NormalizeFontKey(fontName);

            if (map.TryGetValue(fontName, out var c1)){
                path = c1.Path;
                return true;
            }

            if (map.TryGetValue(key, out var c2)){
                path = c2.Path;
                return true;
            }

            return false;
        }

        private void Add(string alias, string path){
            if (string.IsNullOrWhiteSpace(alias)) return;

            var a1 = alias.Trim();
            var a2 = NormalizeFontKey(a1);

            Upsert(a1, path);
            Upsert(a2, path);
        }

        private void Upsert(string key, string path){
            if (string.IsNullOrWhiteSpace(key)) return;

            var cand = new Candidate(path, GetScore(path));
            if (map.TryGetValue(key, out var existing)){
                if (cand.Score > existing.Score)
                    map[key] = cand;
            } else{
                map[key] = cand;
            }
        }

        private static int GetScore(string path){
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch{
                ".ttf" => 100,
                ".otf" => 95,
                ".ttc" => 90,
                ".otc" => 85,
                ".woff" => 40,
                ".woff2" => 35,
                _ => 0
            };
        }

        private static IEnumerable<FontDescription> LoadDescriptions(string fontPath){
            var ext = Path.GetExtension(fontPath).ToLowerInvariant();
            if (ext is ".ttc" or ".otc")
                return FontDescription.LoadFontCollectionDescriptions(fontPath);

            return new[]{ FontDescription.LoadDescription(fontPath) };
        }

        private static IEnumerable<string> BuildAliases(FontDescription d){
            var family = d.FontFamilyInvariantCulture.Trim();
            var sub = d.FontSubFamilyNameInvariantCulture.Trim(); // Regular/Bold/Italic
            var full = d.FontNameInvariantCulture.Trim(); // "Family Subfamily"

            if (!string.IsNullOrWhiteSpace(family)) yield return family;
            if (!string.IsNullOrWhiteSpace(full)) yield return full;

            if (!string.IsNullOrWhiteSpace(family) &&
                !string.IsNullOrWhiteSpace(sub) &&
                !sub.Equals("Regular", StringComparison.OrdinalIgnoreCase)){
                yield return $"{family} {sub}";
            }
        }

        private readonly record struct Candidate(string Path, int Score);
    }
}

public class SubtitleFonts{
    public LanguageItem Language{ get; set; } = new();
    public Dictionary<string, string> Fonts{ get; set; } = new();
}