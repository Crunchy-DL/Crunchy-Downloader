using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

    private readonly FontIndex index = new();
    private int _fontSourceNoticePrinted;

    private void EnsureIndex(string fontsDir){
        index.Rebuild(GetFontSearchDirectories(fontsDir));
    }

    public Task GetFontsAsync(){
        try{
            Directory.CreateDirectory(CfgManager.PathFONTS_DIR);
        } catch (Exception e){
            Console.Error.WriteLine($"Failed to create fonts directory '{CfgManager.PathFONTS_DIR}': {e.Message}");
        }

        if (Interlocked.Exchange(ref _fontSourceNoticePrinted, 1) == 0){
            Console.WriteLine("Crunchyroll-hosted subtitle fonts are no longer available.");
            Console.WriteLine($"Font muxing now uses local fonts from '{CfgManager.PathFONTS_DIR}' and system font directories.");
            Console.WriteLine("Copy any missing subtitle fonts into the local fonts folder if muxing reports them as missing.");
        }

        return Task.CompletedTask;
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
            MainWindow.Instance.ShowError($"Missing Fonts:\n{string.Join(", ", missing)}\n\nAdd the missing font files to:\n{CfgManager.PathFONTS_DIR}");

        return fontsList;
    }

    private bool TryResolveFontPath(string requestedName, string fontsDir, out string resolvedPath, out bool isExactMatch){
        resolvedPath = string.Empty;
        isExactMatch = true;

        var req = NormalizeFontKey(requestedName);

        if (index.TryResolve(req, out resolvedPath))
            return true;

        if (Fonts.TryGetValue(req, out var crFile)){
            var p = FindKnownFontFile(crFile, fontsDir);
            if (!string.IsNullOrEmpty(p)){
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
                var p = FindKnownFontFile(crFamilyFile, fontsDir);
                if (!string.IsNullOrEmpty(p)){
                    resolvedPath = p;
                    return true;
                }
            }
        }

        var reqNoSpace = RemoveSpaces(req);

        foreach (var kv in Fonts){
            if (RemoveSpaces(kv.Key).Equals(reqNoSpace, StringComparison.OrdinalIgnoreCase)){
                var p = FindKnownFontFile(kv.Value, fontsDir);
                if (!string.IsNullOrEmpty(p)){
                    resolvedPath = p;
                    isExactMatch = false;
                    return true;
                }
            }
        }

        return false;
    }

    private static string StripStyleSuffix(string name){
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var styleWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase){
            "Bold", "Italic", "Oblique", "Regular", "Black",
            "Light", "Medium", "Semi", "Condensed"
        };

        var filtered = parts.Where(p => !styleWords.Contains(p)).ToList();

        return filtered.Count > 0 ? string.Join(" ", filtered) : name;
    }

    private static string NormalizeFontKey(string s){
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        s = s.Trim().Trim('"');

        if (s.StartsWith("@"))
            s = s.Substring(1);

        // Convert camel case (TimesNewRoman → Times New Roman)
        s = Regex.Replace(s, @"(?<=[a-z])([A-Z])", " $1");

        // unify separators
        s = s.Replace('_', ' ').Replace('-', ' ');

        // remove MT suffix (ArialMT → Arial)
        s = Regex.Replace(s, @"MT$", "", RegexOptions.IgnoreCase);

        // collapse spaces
        s = Regex.Replace(s, @"\s+", " ").Trim();

        return s;
    }

    private static string RemoveSpaces(string s)
        => s.Replace(" ", "");

    private static string MakeUniqueAttachmentName(string path, List<ParsedFont> existing){
        var baseName = Path.GetFileName(path);

        if (existing.All(e => !baseName.Equals(e.Name, StringComparison.OrdinalIgnoreCase)))
            return baseName;

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(path)))
            .Substring(0, 8)
            .ToLowerInvariant();

        return $"{hash}-{baseName}";
    }

    private static IEnumerable<string> GetFontSearchDirectories(string fontsDir){
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new List<string>();

        void AddIfUsable(string? dir){
            if (string.IsNullOrWhiteSpace(dir))
                return;

            try{
                var fullPath = Path.GetFullPath(dir);
                if (Directory.Exists(fullPath) && seen.Add(fullPath))
                    paths.Add(fullPath);
            } catch{
                // ignore invalid paths
            }
        }

        AddIfUsable(fontsDir);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
            AddIfUsable(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)){
            AddIfUsable("/System/Library/Fonts");
            AddIfUsable("/Library/Fonts");
            AddIfUsable(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts"));
        } else{
            AddIfUsable("/usr/share/fonts");
            AddIfUsable("/usr/local/share/fonts");
            AddIfUsable(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts"));
            AddIfUsable(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "fonts"));
        }

        return paths;
    }

    private static string FindKnownFontFile(string fileName, string fontsDir){
        foreach (var dir in GetFontSearchDirectories(fontsDir)){
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path))
                return path;
        }

        return string.Empty;
    }


    private sealed class FontIndex{
        private readonly Dictionary<string, Candidate> map = new(StringComparer.OrdinalIgnoreCase);

        public void Rebuild(IEnumerable<string> fontDirs){
            map.Clear();
            foreach (var fontsDir in fontDirs){
                if (!Directory.Exists(fontsDir))
                    continue;

                try{
                    foreach (var path in Directory.EnumerateFiles(fontsDir, "*.*", SearchOption.AllDirectories)){
                        var ext = Path.GetExtension(path).ToLowerInvariant();
                        if (ext is not (".ttf" or ".otf" or ".ttc" or ".otc" or ".woff" or ".woff2"))
                            continue;

                        try{
                            foreach (var desc in LoadDescriptions(path)){
                                foreach (var alias in BuildAliases(desc)){
                                    Add(alias, path);
                                }
                            }
                        } catch (Exception e){
                            Console.Error.WriteLine($"Failed to inspect font '{path}': {e.Message}");
                        }
                    }
                } catch (Exception e){
                    Console.Error.WriteLine($"Failed to scan font directory '{fontsDir}': {e.Message}");
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
            var family = d.FontFamilyInvariantCulture?.Trim() ?? string.Empty;
            var sub = d.FontSubFamilyNameInvariantCulture?.Trim() ?? string.Empty; // Regular/Bold/Italic
            var full = d.FontNameInvariantCulture?.Trim() ?? string.Empty; // "Family Subfamily"

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
