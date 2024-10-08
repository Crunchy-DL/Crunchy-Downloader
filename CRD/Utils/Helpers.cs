using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Ffmpeg_Encoding;
using CRD.Utils.JsonConv;
using CRD.Utils.Structs;
using CRD.Utils.Structs.Crunchyroll.Music;
using Newtonsoft.Json;

namespace CRD.Utils;

public class Helpers{
    public static T? Deserialize<T>(string json, JsonSerializerSettings? serializerSettings){
        try{
            serializerSettings ??= new JsonSerializerSettings();
            serializerSettings.Converters.Add(new UtcToLocalTimeConverter());

            return JsonConvert.DeserializeObject<T>(json, serializerSettings);
        } catch (JsonException ex){
            Console.Error.WriteLine($"Error deserializing JSON: {ex.Message}");
            throw;
        }
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
        string[] directories = directoryPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Start the loop from the correct initial index
        int startIndex = isAbsolute && directories.Length > 0 && string.IsNullOrEmpty(directories[0]) ? 2 : 0;

        for (int i = startIndex; i < directories.Length; i++){
            // Skip empty parts (which can occur with UNC paths)
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

    public static async Task<(bool IsOk, int ErrorCode)> RunFFmpegWithPresetAsync(string inputFilePath, VideoPreset preset){
        try{
            string outputExtension = Path.GetExtension(inputFilePath);
            string directory = Path.GetDirectoryName(inputFilePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputFilePath);
            string tempOutputFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}_output{outputExtension}");

            string additionalParams = string.Join(" ", preset.AdditionalParameters);
            string qualityOption;
            if (preset.Codec == "h264_nvenc" || preset.Codec == "hevc_nvenc"){
                qualityOption = $"-cq {preset.Crf}"; // For NVENC
            } else if (preset.Codec == "h264_qsv" || preset.Codec == "hevc_qsv"){
                qualityOption = $"-global_quality {preset.Crf}"; // For Intel QSV
            } else if (preset.Codec == "h264_amf" || preset.Codec == "hevc_amf"){
                qualityOption = $"-qp {preset.Crf}"; // For AMD VCE
            } else{
                qualityOption = $"-crf {preset.Crf}"; // For software codecs like libx264/libx265
            }

            string ffmpegCommand = $"-loglevel warning -i \"{inputFilePath}\" -c:v {preset.Codec} {qualityOption} -vf \"scale={preset.Resolution},fps={preset.FrameRate}\" {additionalParams} \"{tempOutputFilePath}\"";
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
                }

                return (IsOk: isSuccess, ErrorCode: process.ExitCode);
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
            return (IsOk: false, ErrorCode: -1);
        }
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
        // Regular expression pattern to match |S followed by a number and optionally C followed by another number
        string pattern = @"\|S(\d+)(?:C(\d+))?";
        Match match = Regex.Match(input, pattern);

        if (match.Success){
            string sNumber = match.Groups[1].Value;
            string cNumber = match.Groups[2].Value;

            if (!string.IsNullOrEmpty(cNumber)){
                return $"{sNumber}.{cNumber}";
            } else{
                return sNumber;
            }
        } else{
            return null;
        }
    }


    public static async Task<Bitmap?> LoadImage(string imageUrl,int desiredWidth = 0,int desiredHeight = 0){
        try{
            using (var client = new HttpClient()){
                var response = await client.GetAsync(imageUrl);
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
            }
        } catch (Exception ex){
            Console.Error.WriteLine("Failed to load image: " + ex.Message);
        }

        return null;
    }

    public static Dictionary<string, List<DownloadedMedia>> GroupByLanguageWithSubtitles(List<DownloadedMedia> allMedia){
        //Group by language
        var languageGroups = allMedia
            .Where(media =>
                !string.IsNullOrEmpty(media.Lang.CrLocale) ||
                (media.Type == DownloadMediaType.Subtitle && media.RelatedVideoDownloadMedia != null &&
                 !string.IsNullOrEmpty(media.RelatedVideoDownloadMedia.Lang.CrLocale))
            )
            .GroupBy(media => {
                if (media.Type == DownloadMediaType.Subtitle && media.RelatedVideoDownloadMedia != null){
                    return media.RelatedVideoDownloadMedia.Lang.CrLocale;
                }

                return media.Lang.CrLocale;
            })
            .ToDictionary(group => group.Key, group => group.ToList());

        //Find and add Description media to each group
        var descriptionMedia = allMedia.Where(media => media.Type == DownloadMediaType.Description).ToList();

        foreach (var description in descriptionMedia){
            foreach (var group in languageGroups.Values){
                group.Add(description);
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
}