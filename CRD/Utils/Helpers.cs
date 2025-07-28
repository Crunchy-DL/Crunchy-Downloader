using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
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

        foreach (var property in originalRequest.Properties){
            clone.Properties.Add(property);
        }

        return clone;
    }

    public static T DeepCopy<T>(T obj){
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


    public static string ConvertTimeFormat(string time){
        var timeParts = time.Split(':', '.');
        int hours = int.Parse(timeParts[0]);
        int minutes = int.Parse(timeParts[1]);
        int seconds = int.Parse(timeParts[2]);
        int milliseconds = int.Parse(timeParts[3]);

        return $"{hours}:{minutes:D2}:{seconds:D2}.{milliseconds / 10:D2}";
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

    public static Locale ConvertStringToLocale(string? value){
        foreach (Locale locale in Enum.GetValues(typeof(Locale))){
            var type = typeof(Locale);
            var memInfo = type.GetMember(locale.ToString());
            var attributes = memInfo[0].GetCustomAttributes(typeof(EnumMemberAttribute), false);
            var description = ((EnumMemberAttribute)attributes[0]).Value;

            if (description == value){
                return locale;
            }
        }

        if (string.IsNullOrEmpty(value)){
            return Locale.DefaulT;
        }

        return Locale.Unknown; // Return default if not found
    }

    public static string GenerateSessionId(){
        // Get UTC milliseconds
        var utcNow = DateTime.UtcNow;
        var milliseconds = utcNow.Millisecond.ToString().PadLeft(3, '0');

        // Get a high-resolution timestamp
        long timestamp = Stopwatch.GetTimestamp();
        double timestampToMilliseconds = (double)timestamp / Stopwatch.Frequency * 1000;
        string highResTimestamp = timestampToMilliseconds.ToString("F0").PadLeft(13, '0');

        return milliseconds + highResTimestamp;
    }

    public static void ConvertChapterFileForFFMPEG(string chapterFilePath){
        var chapterLines = File.ReadAllLines(chapterFilePath);
        var ffmpegChapterLines = new List<string>{ ";FFMETADATA1" };
        var chapters = new List<(double StartTime, string Title)>();

        for (int i = 0; i < chapterLines.Length; i += 2){
            var timeLine = chapterLines[i];
            var nameLine = chapterLines[i + 1];

            var timeParts = timeLine.Split('=');
            var nameParts = nameLine.Split('=');

            if (timeParts.Length == 2 && nameParts.Length == 2){
                var startTime = TimeSpan.Parse(timeParts[1]).TotalMilliseconds;
                var title = nameParts[1];
                chapters.Add((startTime, title));
            }
        }

        // Sort chapters by start time
        chapters = chapters.OrderBy(c => c.StartTime).ToList();

        for (int i = 0; i < chapters.Count; i++){
            var startTime = chapters[i].StartTime;
            var title = chapters[i].Title;
            var endTime = (i + 1 < chapters.Count) ? chapters[i + 1].StartTime : startTime + 10000; // Add 10 seconds to the last chapter end time

            if (endTime < startTime){
                endTime = startTime + 10000; // Correct end time if it is before start time
            }

            ffmpegChapterLines.Add("[CHAPTER]");
            ffmpegChapterLines.Add("TIMEBASE=1/1000");
            ffmpegChapterLines.Add($"START={startTime}");
            ffmpegChapterLines.Add($"END={endTime}");
            ffmpegChapterLines.Add($"title={title}");
        }

        File.WriteAllLines(chapterFilePath, ffmpegChapterLines);
    }

    public static async Task<(bool IsOk, int ErrorCode)> ExecuteCommandAsync(string type, string bin, string command){
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

    private static string GetQualityOption(VideoPreset preset){
        return preset.Codec switch{
            "h264_nvenc" or "hevc_nvenc" => $"-cq {preset.Crf}", // For NVENC
            "h264_qsv" or "hevc_qsv" => $"-global_quality {preset.Crf}", // For Intel QSV
            "h264_amf" or "hevc_amf" => $"-qp {preset.Crf}", // For AMD VCE
            _ => $"-crf {preset.Crf}", // For software codecs like libx264/libx265
        };
    }

    public static async Task<(bool IsOk, int ErrorCode)> RunFFmpegWithPresetAsync(string inputFilePath, VideoPreset preset, CrunchyEpMeta? data = null){
        try{
            string outputExtension = Path.GetExtension(inputFilePath);
            string directory = Path.GetDirectoryName(inputFilePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputFilePath);
            string tempOutputFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}_output{outputExtension}");

            string additionalParams = string.Join(" ", preset.AdditionalParameters.Select(param => {
                var splitIndex = param.IndexOf(' ');
                if (splitIndex > 0){
                    var prefix = param[..splitIndex];
                    var value = param[(splitIndex + 1)..];

                    if (value.Contains(' ') && !(value.StartsWith("\"") && value.EndsWith("\""))){
                        value = $"\"{value}\"";
                    }

                    return $"{prefix} {value}";
                }

                return param;
            }));

            string qualityOption = GetQualityOption(preset);

            TimeSpan? totalDuration = await GetMediaDurationAsync(CfgManager.PathFFMPEG, inputFilePath);
            if (totalDuration == null){
                Console.Error.WriteLine("Unable to retrieve input file duration.");
            } else{
                Console.WriteLine($"Total Duration: {totalDuration}");
            }


            string ffmpegCommand = $"-loglevel info -i \"{inputFilePath}\" -c:v {preset.Codec} {qualityOption} -vf \"scale={preset.Resolution},fps={preset.FrameRate}\" {additionalParams} \"{tempOutputFilePath}\"";
            using (var process = new Process()){
                process.StartInfo.FileName = CfgManager.PathFFMPEG;
                process.StartInfo.Arguments = ffmpegCommand;
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
                        if (data != null && totalDuration != null){
                            ParseProgress(e.Data, totalDuration.Value, data);
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                bool isSuccess = process.ExitCode == 0;

                if (isSuccess){
                    // Delete the original input file
                    File.Delete(inputFilePath);

                    // Rename the output file to the original name
                    File.Move(tempOutputFilePath, inputFilePath);
                } else{
                    // If something went wrong, delete the temporary output file
                    File.Delete(tempOutputFilePath);
                    Console.Error.WriteLine("FFmpeg processing failed.");
                    Console.Error.WriteLine($"Command: {ffmpegCommand}");
                }

                return (IsOk: isSuccess, ErrorCode: process.ExitCode);
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
            return (IsOk: false, ErrorCode: -1);
        }
    }

    private static void ParseProgress(string progressString, TimeSpan totalDuration, CrunchyEpMeta data){
        try{
            if (progressString.Contains("time=")){
                var timeIndex = progressString.IndexOf("time=") + 5;
                var timeString = progressString.Substring(timeIndex, 11);


                if (TimeSpan.TryParse(timeString, out var currentTime)){
                    int progress = (int)(currentTime.TotalSeconds / totalDuration.TotalSeconds * 100);
                    Console.WriteLine($"Progress: {progress:F2}%");

                    data.DownloadProgress = new DownloadProgress(){
                        IsDownloading = true,
                        Percent = progress,
                        Time = 0,
                        DownloadSpeed = 0,
                        Doing = "Encoding"
                    };

                    QueueManager.Instance.Queue.Refresh();
                }
            }
        } catch (Exception e){
            Console.Error.WriteLine("Failed to calculate encoding progess");
            Console.Error.WriteLine(e.Message);
        }
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

    private static readonly char[] Delimiters ={ ' ', ',', '.', ';', ':', '-', '_', '\'' };

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
            .Where(media => media.Type != DownloadMediaType.Description &&
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

        return languageGroups;
    }

    public static string GetValidFolderName(string folderName){
        // Get the invalid characters for a folder name
        char[] invalidChars = Path.GetInvalidFileNameChars();

        // Check if the folder name contains any invalid characters
        bool isValid = !folderName.Any(c => invalidChars.Contains(c));

        // Check for reserved names on Windows
        string[] reservedNames =["CON", "PRN", "AUX", "NUL", "COM1", "LPT1"];
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
        Dictionary<string, ServerData> target,
        Dictionary<string, ServerData> source){
        foreach (var kvp in source){
            if (target.TryGetValue(kvp.Key, out var existing)){
                // Merge audio
                existing.audio ??=[];
                if (kvp.Value.audio != null)
                    existing.audio.AddRange(kvp.Value.audio);

                // Merge video
                existing.video ??=[];
                if (kvp.Value.video != null)
                    existing.video.AddRange(kvp.Value.video);
            } else{
                // Add new entry (clone lists to avoid reference issues)
                target[kvp.Key] = new ServerData{
                    audio = kvp.Value.audio != null ? new List<AudioPlaylist>(kvp.Value.audio) : new List<AudioPlaylist>(),
                    video = kvp.Value.video != null ? new List<VideoPlaylist>(kvp.Value.video) : new List<VideoPlaylist>()
                };
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
}