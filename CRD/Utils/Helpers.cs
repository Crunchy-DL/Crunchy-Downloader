using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CRD.Downloader;
using CRD.Utils.Ffmpeg_Encoding;
using CRD.Utils.Files;
using CRD.Utils.HLS;
using CRD.Utils.JsonConv;
using CRD.Utils.Parser;
using CRD.Utils.Structs;
using FluentAvalonia.UI.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CRD.Utils;

public class Helpers{
    public static T? Deserialize<T>(string json, JsonSerializerSettings? serializerSettings){
        try{
            serializerSettings ??= new JsonSerializerSettings();
            serializerSettings.Converters.Add(new UtcToLocalTimeConverter());

            return JsonConvert.DeserializeObject<T>(json, serializerSettings);
        } catch (JsonException ex){
            Console.Error.WriteLine($"Error deserializing JSON: {ex.Message}");
        }

        return default;
    }

    public static HttpRequestMessage CloneHttpRequestMessage(HttpRequestMessage originalRequest){
        var clone = new HttpRequestMessage(originalRequest.Method, originalRequest.RequestUri){
            Content = originalRequest.Content?.Clone(),
            Version = originalRequest.Version
        };
        foreach (var header in originalRequest.Headers){
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var kvp in originalRequest.Options){
            var key = new HttpRequestOptionsKey<object?>(kvp.Key);
            clone.Options.Set(key, kvp.Value);
        }

        return clone;
    }

    public static T? DeepCopy<T>(T obj){
        var settings = new JsonSerializerSettings{
            ContractResolver = new DefaultContractResolver{
                IgnoreSerializableAttribute = true,
                IgnoreSerializableInterface = true
            },
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        var json = JsonConvert.SerializeObject(obj, settings);
        return JsonConvert.DeserializeObject<T>(json);
    }

    public static int ToKbps(int bps) => (int)Math.Round(bps / 1000.0);

    public static int SnapToAudioBucket(int kbps){
        int[] buckets = { 64, 96, 128, 192, 256 };
        return buckets.OrderBy(b => Math.Abs(b - kbps)).First();
    }

    public static int WidthBucket(int width, int height){
        int expected = (int)Math.Round(height * 16 / 9.0);
        int tol = Math.Max(8, (int)(expected * 0.02)); // ~2% or ≥8 px
        return Math.Abs(width - expected) <= tol ? expected : width;
    }

    public static string ConvertTimeFormat(string vttTime){
        if (TimeSpan.TryParseExact(vttTime, @"hh\:mm\:ss\.fff", null, out var ts) ||
            TimeSpan.TryParseExact(vttTime, @"mm\:ss\.fff", null, out ts)){
            var totalCentiseconds = (int)Math.Round(ts.TotalMilliseconds / 10.0, MidpointRounding.AwayFromZero);
            var hours = totalCentiseconds / 360000; // 100 cs * 60 * 60
            var rem = totalCentiseconds % 360000;
            var mins = rem / 6000;
            rem %= 6000;
            var secs = rem / 100;
            var cs = rem % 100;
            return $"{hours}:{mins:00}:{secs:00}.{cs:00}";
        }

        return "0:00:00.00";
    }

    public static string ConvertVTTStylesToASS(string dialogue){
        dialogue = Regex.Replace(dialogue, @"<b>", "{\\b1}");
        dialogue = Regex.Replace(dialogue, @"</b>", "{\\b0}");
        dialogue = Regex.Replace(dialogue, @"<i>", "{\\i1}");
        dialogue = Regex.Replace(dialogue, @"</i>", "{\\i0}");
        dialogue = Regex.Replace(dialogue, @"<u>", "{\\u1}");
        dialogue = Regex.Replace(dialogue, @"</u>", "{\\u0}");

        dialogue = Regex.Replace(dialogue, @"<[^>]+>", ""); // Remove any other HTML-like tags

        return dialogue;
    }

    public static void OpenUrl(string url){
        try{
            Process.Start(new ProcessStartInfo{
                FileName = url,
                UseShellExecute = true
            });
        } catch (Exception e){
            Console.Error.WriteLine($"An error occurred while trying to open URL - {url} : {e.Message}");
        }
    }

    public static void EnsureDirectoriesExist(string path){
        // Console.WriteLine($"Check if path exists: {path}");

        // Check if the path is absolute
        bool isAbsolute = Path.IsPathRooted(path);

        // Get all directory parts of the path except the last segment (assuming it's a file)
        string directoryPath = Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(directoryPath)){
            Console.WriteLine("The provided path does not contain any directory information.");
            return;
        }

        // Initialize the cumulative path based on whether the original path is absolute or not
        string cumulativePath = isAbsolute ? Path.GetPathRoot(directoryPath) : Environment.CurrentDirectory;

        // Get all directory parts
        string[] directories = directoryPath.Split(Path.DirectorySeparatorChar);

        // Start the loop from the correct initial index
        int startIndex = isAbsolute && directories.Length > 0 && string.IsNullOrEmpty(directories[0]) ? 1 : 0;

        if (isAbsolute && cumulativePath == "/"){
            cumulativePath = "/";
        }

        for (int i = startIndex; i < directories.Length; i++){
            // Skip empty parts
            if (string.IsNullOrEmpty(directories[i])){
                continue;
            }

            // Build the path incrementally
            cumulativePath = Path.Combine(cumulativePath, directories[i]);

            // Check if the directory exists and create it if it does not
            if (!Directory.Exists(cumulativePath)){
                Directory.CreateDirectory(cumulativePath);
                Console.WriteLine($"Created directory: {cumulativePath}");
            }
        }
    }


    public static bool IsValidPath(string path){
        char[] invalidChars = Path.GetInvalidPathChars();

        if (string.IsNullOrWhiteSpace(path)){
            return false;
        }

        if (path.Any(ch => invalidChars.Contains(ch))){
            return false;
        }

        try{
            // Use Path.GetFullPath to ensure that the path can be fully qualified
            string fullPath = Path.GetFullPath(path);
            return true;
        } catch (Exception){
            return false;
        }
    }
    
    public static async Task<(bool IsOk, int ErrorCode)> ExecuteCommandAsync(string bin, string command){
        try{
            using (var process = new Process()){
                process.StartInfo.FileName = bin;
                process.StartInfo.Arguments = command;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        if (e.Data.StartsWith("Error:")){
                            Console.Error.WriteLine(e.Data);
                        } else{
                            Console.WriteLine(e.Data);
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        Console.Error.WriteLine($"{e.Data}");
                    }
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                bool isSuccess = process.ExitCode == 0;

                return (IsOk: isSuccess, ErrorCode: process.ExitCode);
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
            return (IsOk: false, ErrorCode: -1);
        }
    }

    public static void DeleteFile(string filePath){
        if (string.IsNullOrEmpty(filePath)){
            return;
        }

        try{
            if (File.Exists(filePath)){
                File.Delete(filePath);
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"Failed to delete file {filePath}. Error: {ex.Message}");
        }
    }

    public static async Task<(bool IsOk, int ErrorCode)> ExecuteCommandAsyncWorkDir(string type, string bin, string command, string workingDir){
        try{
            using (var process = new Process()){
                process.StartInfo.WorkingDirectory = workingDir;
                process.StartInfo.FileName = bin;
                process.StartInfo.Arguments = command;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        Console.WriteLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        Console.Error.WriteLine($"{e.Data}");
                    }
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                bool isSuccess = process.ExitCode == 0;

                return (IsOk: isSuccess, ErrorCode: process.ExitCode);
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
            return (IsOk: false, ErrorCode: -1);
        }
    }

    private static IEnumerable<string> GetQualityOption(VideoPreset preset){
        return preset.Codec switch{
            "h264_nvenc" or "hevc_nvenc" =>["-cq", preset.Crf.ToString()],
            "h264_qsv" or "hevc_qsv" =>["-global_quality", preset.Crf.ToString()],
            "h264_amf" or "hevc_amf" =>["-qp", preset.Crf.ToString()],
            _ =>["-crf", preset.Crf.ToString()]
        };
    }

    public static async Task<(bool IsOk, int ErrorCode)> RunFFmpegWithPresetAsync(
        string inputFilePath,
        VideoPreset preset,
        CrunchyEpMeta? data = null){
        try{
            string ext = Path.GetExtension(inputFilePath);
            string dir = Path.GetDirectoryName(inputFilePath)!;
            string name = Path.GetFileNameWithoutExtension(inputFilePath);

            string tempOutput = Path.Combine(dir, $"{name}_output{ext}");

            TimeSpan? totalDuration = await GetMediaDurationAsync(CfgManager.PathFFMPEG, inputFilePath);

            var args = new List<string>{
                "-nostdin",
                "-hide_banner",
                "-loglevel", "error",
                "-i", inputFilePath,
            };

            if (!string.IsNullOrWhiteSpace(preset.Codec)){
                args.Add("-c:v");
                args.Add(preset.Codec);
            }

            args.AddRange(GetQualityOption(preset));

            args.Add("-vf");
            args.Add($"scale={preset.Resolution},fps={preset.FrameRate}");

            foreach (var param in preset.AdditionalParameters){
                args.AddRange(SplitArguments(param));
            }

            args.Add(tempOutput);

            string commandString = BuildCommandString(CfgManager.PathFFMPEG, args);
            int exitCode;
            try{
                exitCode = await RunFFmpegAsync(
                    CfgManager.PathFFMPEG,
                    args,
                    data?.Cts.Token ?? CancellationToken.None,
                    onStdErr: line => { Console.Error.WriteLine(line); },
                    onStdOut: Console.WriteLine
                );
            } catch (OperationCanceledException){
                if (File.Exists(tempOutput)){
                    try{
                        File.Delete(tempOutput);
                    } catch{
                        // ignored
                    }
                }

                Console.Error.WriteLine("FFMPEG task was canceled");
                return (false, -2);
            }

            bool success = exitCode == 0;

            if (success){
                File.Delete(inputFilePath);
                File.Move(tempOutput, inputFilePath);
            } else{
                if (File.Exists(tempOutput)){
                    File.Delete(tempOutput);
                }

                Console.Error.WriteLine("FFmpeg processing failed.");
                Console.Error.WriteLine("Command:");
                Console.Error.WriteLine(commandString);
            }


            return (success, exitCode);
        } catch (Exception ex){
            Console.Error.WriteLine(ex);

            return (false, -1);
        }
    }

    private static IEnumerable<string> SplitArguments(string commandLine){
        var args = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in commandLine){
            if (c == '"'){
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes){
                if (current.Length > 0){
                    args.Add(current.ToString());
                    current.Clear();
                }
            } else{
                current.Append(c);
            }
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return args;
    }

    private static string BuildCommandString(string exe, IEnumerable<string> args){
        static string Quote(string s){
            if (string.IsNullOrWhiteSpace(s))
                return "\"\"";

            return s.Contains(' ') || s.Contains('"')
                ? $"\"{s.Replace("\"", "\\\"")}\""
                : s;
        }

        return exe + " " + string.Join(" ", args.Select(Quote));
    }

    public static async Task<int> RunFFmpegAsync(
        string ffmpegPath,
        IEnumerable<string> args,
        CancellationToken token,
        Action<string>? onStdErr = null,
        Action<string>? onStdOut = null){
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo{
            FileName = ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.Start();

        // capture streams instead of process
        var stdout = process.StandardOutput;
        var stderr = process.StandardError;

        async Task ReadStreamAsync(StreamReader reader, Action<string>? callback){
            while (await reader.ReadLineAsync(token) is{ } line)
                callback?.Invoke(line);
        }

        var stdoutTask = ReadStreamAsync(stdout, onStdOut);
        var stderrTask = ReadStreamAsync(stderr, onStdErr);

        var proc = process;

        await using var reg = token.Register(() => {
            try{
                proc.Kill(true);
            } catch{
                // ignored
            }
        });

        try{
            await process.WaitForExitAsync(token);
        } catch (OperationCanceledException){
            try{
                if (!process.HasExited)
                    process.Kill(true);
            } catch{
                // ignored
            }

            throw;
        }

        await Task.WhenAll(stdoutTask, stderrTask);

        return process.ExitCode;
    }


    public static async Task<TimeSpan?> GetMediaDurationAsync(string ffmpegPath, string inputFilePath){
        try{
            using (var process = new Process()){
                process.StartInfo.FileName = ffmpegPath;
                process.StartInfo.Arguments = $"-i \"{inputFilePath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                string output = string.Empty;
                process.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        output += e.Data + Environment.NewLine;
                    }
                };

                process.Start();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                Regex regex = new Regex(@"Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})");
                Match match = regex.Match(output);
                if (match.Success){
                    int hours = int.Parse(match.Groups[1].Value);
                    int minutes = int.Parse(match.Groups[2].Value);
                    double seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

                    return new TimeSpan(hours, minutes, (int)seconds);
                }
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred while retrieving media duration: {ex.Message}");
        }

        return null;
    }

    public static double CalculateCosineSimilarity(string text1, string text2){
        var vector1 = ComputeWordFrequency(text1);
        var vector2 = ComputeWordFrequency(text2);

        return CosineSimilarity(vector1, vector2);
    }

    private static readonly char[] Delimiters = { ' ', ',', '.', ';', ':', '-', '_', '\'' };

    public static Dictionary<string, double> ComputeWordFrequency(string text){
        var wordFrequency = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var words = SplitText(text);

        foreach (var word in words){
            if (wordFrequency.TryGetValue(word, out double count)){
                wordFrequency[word] = count + 1;
            } else{
                wordFrequency[word] = 1;
            }
        }

        return wordFrequency;
    }

    private static List<string> SplitText(string text){
        var words = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++){
            if (Array.IndexOf(Delimiters, text[i]) >= 0){
                if (i > start){
                    words.Add(text.Substring(start, i - start));
                }

                start = i + 1;
            }
        }

        if (start < text.Length){
            words.Add(text.Substring(start));
        }

        return words;
    }


    private static double CosineSimilarity(Dictionary<string, double> vector1, Dictionary<string, double> vector2){
        var intersection = vector1.Keys.Intersect(vector2.Keys);

        double dotProduct = intersection.Sum(term => vector1[term] * vector2[term]);
        double normA = Math.Sqrt(vector1.Values.Sum(val => val * val));
        double normB = Math.Sqrt(vector2.Values.Sum(val => val * val));

        if (normA == 0 || normB == 0){
            // If either vector has zero length, return 0 similarity.
            return 0;
        }

        return dotProduct / (normA * normB);
    }

    public static string? ExtractNumberAfterS(string input){
        // Regular expression pattern to match |S followed by a number and optionally C or P followed by another number
        string pattern = @"\|S(\d+)(?:C(\d+)|P(\d+))?";
        Match match = Regex.Match(input, pattern);

        if (match.Success){
            string sNumber = match.Groups[1].Value; // Extract the S number
            string cNumber = match.Groups[2].Value; // Extract the C number if present
            string pNumber = match.Groups[3].Value; // Extract the P number if present

            if (int.TryParse(sNumber, out int sNumericBig)){
                // Reject invalid S numbers (>= 1000)
                if (sNumericBig >= 1000)
                    return null;
            }

            if (!string.IsNullOrEmpty(cNumber)){
                // Case for C: Return S + . + C
                return $"{sNumber}.{cNumber}";
            } else if (!string.IsNullOrEmpty(pNumber)){
                // Case for P: Increment S by P - 1
                if (int.TryParse(sNumber, out int sNumeric) && int.TryParse(pNumber, out int pNumeric)){
                    return (sNumeric + (pNumeric - 1)).ToString();
                }
            } else{
                // Return only S if no C or P is present
                return sNumber;
            }
        }

        return null;
    }


    public static async Task<Bitmap?> LoadImage(string imageUrl, int desiredWidth = 0, int desiredHeight = 0){
        try{
            var response = await HttpClientReq.Instance.GetHttpClient().GetAsync(imageUrl);

            if (ChallengeDetector.IsClearanceRequired(response)){
                Console.Error.WriteLine($"Cloudflare Challenge detected ");
            }

            response.EnsureSuccessStatusCode();
            using (var stream = await response.Content.ReadAsStreamAsync()){
                var bitmap = new Bitmap(stream);

                if (desiredWidth != 0 && desiredHeight != 0){
                    var scaledBitmap = bitmap.CreateScaledBitmap(new PixelSize(desiredWidth, desiredHeight));

                    bitmap.Dispose();

                    return scaledBitmap;
                }


                return bitmap;
            }
        } catch (Exception ex){
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
        }

        return null;
    }

    public static Dictionary<string, List<DownloadedMedia>> GroupByLanguageWithSubtitles(List<DownloadedMedia> allMedia){
        //Group by language
        var languageGroups = allMedia
            .Where(media => media.Type != DownloadMediaType.Description && media.Type != DownloadMediaType.Cover &&
                            (!string.IsNullOrEmpty(media.Lang?.CrLocale) ||
                             (media is{ Type: DownloadMediaType.Subtitle, RelatedVideoDownloadMedia: not null } &&
                              !string.IsNullOrEmpty(media.RelatedVideoDownloadMedia.Lang?.CrLocale)))
            )
            .GroupBy(media => {
                if (media is{ Type: DownloadMediaType.Subtitle, RelatedVideoDownloadMedia: not null }){
                    return media.RelatedVideoDownloadMedia.Lang?.CrLocale ?? "und";
                }

                return media.Lang?.CrLocale ?? "und";
            })
            .ToDictionary(group => group.Key, group => group.ToList());

        //Find and add Description media to each group
        var descriptionMedia = allMedia.Where(media => media.Type == DownloadMediaType.Description).ToList();

        if (descriptionMedia.Count > 0){
            foreach (var group in languageGroups.Values){
                group.Add(descriptionMedia[0]);
            }
        }

        //Find and add Cover media to each group
        var coverMedia = allMedia.Where(media => media.Type == DownloadMediaType.Cover).ToList();

        if (coverMedia.Count > 0){
            foreach (var group in languageGroups.Values){
                group.Add(coverMedia[0]);
            }
        }

        return languageGroups;
    }

    public static string GetValidFolderName(string folderName){
        // Get the invalid characters for a folder name
        char[] invalidChars = Path.GetInvalidFileNameChars();

        // Check if the folder name contains any invalid characters
        bool isValid = !folderName.Any(c => invalidChars.Contains(c));

        // Check for reserved names on Windows
        string[] reservedNames = ["CON", "PRN", "AUX", "NUL", "COM1", "LPT1"];
        bool isReservedName = reservedNames.Contains(folderName.ToUpperInvariant());

        if (isValid && !isReservedName && folderName.Length <= 255){
            return folderName; // Return the original folder name if it's valid
        }

        string uuid = Guid.NewGuid().ToString();
        return uuid;
    }

    public static string LimitFileNameLength(string fileName, int maxFileNameLength){
        string directory = Path.GetDirectoryName(fileName) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        if (name.Length > maxFileNameLength - extension.Length){
            name = name.Substring(0, maxFileNameLength - extension.Length);
        }

        return Path.Combine(directory, name + extension);
    }

    public static string AddUncPrefixIfNeeded(string path){
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !IsLongPathEnabled()){
            if (!string.IsNullOrEmpty(path) && !path.StartsWith(@"\\?\")){
                return $@"\\?\{Path.GetFullPath(path)}";
            }
        }

        return path;
    }


    private static bool IsLongPathEnabled(){
        try{
            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem")){
                if (key != null){
                    var value = key.GetValue("LongPathsEnabled", 0);
                    return value is int intValue && intValue == 1;
                }
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"Failed to check if long paths are enabled: {ex.Message}");
        }

        return false; // Default to false if unable to read the registry
    }


    private static Avalonia.Controls.Image? _backgroundImageLayer;

    public static void SetBackgroundImage(string backgroundImagePath, double? imageOpacity = 0.5, double? blurRadius = 10){
        try{
            var activeWindow = GetActiveWindow();
            if (activeWindow == null)
                return;


            if (activeWindow.Content is not Panel rootPanel){
                rootPanel = new Grid();
                activeWindow.Content = rootPanel;
            }


            if (string.IsNullOrEmpty(backgroundImagePath)){
                if (_backgroundImageLayer != null){
                    rootPanel.Children.Remove(_backgroundImageLayer);
                    _backgroundImageLayer = null;
                }

                return;
            }

            if (_backgroundImageLayer == null){
                _backgroundImageLayer = new Avalonia.Controls.Image{
                    Stretch = Stretch.UniformToFill,
                    ZIndex = -1,
                };
                rootPanel.Children.Add(_backgroundImageLayer);
            }

            _backgroundImageLayer.Source = new Bitmap(backgroundImagePath);
            _backgroundImageLayer.Opacity = imageOpacity ?? 0.5;

            _backgroundImageLayer.Effect = new BlurEffect{
                Radius = blurRadius ?? 10
            };
        } catch (Exception ex){
            Console.WriteLine($"Failed to set background image: {ex.Message}");
        }
    }

    private static Window? GetActiveWindow(){
        // Ensure the application is running with a Classic Desktop Lifetime
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime){
            // Return the first active window found in the desktop application's window list
            return desktopLifetime.Windows.FirstOrDefault(window => window.IsActive);
        }

        return null;
    }


    public static bool IsInstalled(string checkFor, string versionString){
        try{
            // Create a new process for mkvmerge
            Process process = new Process();
            process.StartInfo.FileName = checkFor;
            process.StartInfo.Arguments = versionString; // A harmless command to check if mkvmerge is available
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Start the process and wait for it to exit
            process.Start();
            process.WaitForExit();

            // If the exit code is 0, mkvmerge was found and executed successfully
            return process.ExitCode == 0;
        } catch (Exception){
            // If an exception is caught, mkvmerge is not installed or accessible
            return false;
        }
    }


    public static void MergePlaylistData(
        ServerData target,
        Dictionary<string, ServerData> source,
        bool mergeAudio,
        bool mergeVideo){
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (source == null) throw new ArgumentNullException(nameof(source));

        var serverSet = new HashSet<string>(target.servers);

        void AddServer(string s){
            if (!string.IsNullOrWhiteSpace(s) && serverSet.Add(s))
                target.servers.Add(s);
        }

        foreach (var kvp in source){
            var key = kvp.Key;
            var src = kvp.Value;

            if (!src.servers.Contains(key))
                src.servers.Add(key);

            AddServer(key);
            foreach (var s in src.servers)
                AddServer(s);

            if (mergeAudio && src.audio != null){
                target.audio ??= [];
                target.audio.AddRange(src.audio);
            }

            if (mergeVideo && src.video != null){
                target.video ??= [];
                target.video.AddRange(src.video);
            }
        }
    }


    private static readonly SemaphoreSlim ShutdownLock = new(1, 1);

    public static async Task ShutdownComputer(){
        if (!await ShutdownLock.WaitAsync(0))
            return;
        try{
            var timer = new System.Timers.Timer(30000); // 30 seconds
            timer.Elapsed += (sender, e) => { PerformShutdown(); };
            timer.AutoReset = false;
            timer.Start();

            var dialog = new ContentDialog{
                Title = "Shutdown Pending",
                Content = "The PC will shut down in 30 seconds.\nClick 'Cancel' to abort.",
                PrimaryButtonText = "Cancel Shutdown",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary){
                timer.Stop();
            }
        } catch (Exception e){
            Console.Error.WriteLine(e);
        } finally{
            ShutdownLock.Release();
        }
    }

    private static void PerformShutdown(){
        string shutdownCmd;
        string shutdownArgs;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
            shutdownCmd = "shutdown";
            shutdownArgs = "/s /t 0";
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                   RuntimeInformation.IsOSPlatform(OSPlatform.OSX)){
            shutdownCmd = "shutdown";
            shutdownArgs = "-h now";
        } else{
            throw new PlatformNotSupportedException();
        }

        try{
            using (var process = new Process()){
                process.StartInfo.FileName = shutdownCmd;
                process.StartInfo.Arguments = shutdownArgs;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        Console.Error.WriteLine($"{e.Data}");
                    }
                };

                process.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        Console.Error.WriteLine(e.Data);
                    }
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                if (process.ExitCode != 0){
                    Console.Error.WriteLine($"Shutdown failed with exit code {process.ExitCode}");
                }
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"Failed to start shutdown process: {ex.Message}");
        }
    }

    public static bool ExecuteFile(string filePath){
        try{
            if (Path.GetExtension(filePath).Equals(".ps1", StringComparison.OrdinalIgnoreCase)){
                Process.Start("powershell.exe", $"-ExecutionPolicy Bypass -File \"{filePath}\"");
            } else{
                Process.Start(new ProcessStartInfo{
                    FileName = filePath,
                    UseShellExecute = true
                });
            }

            return true;
        } catch (Exception ex){
            Console.Error.WriteLine($"Execution failed: {ex.Message}");
            return false;
        }
    }
}