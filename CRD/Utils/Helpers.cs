using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CRD.Utils;

public class Helpers{
    /// <summary>
    /// Deserializes a JSON string into a specified .NET type.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="serializerSettings">The settings for deserialization if null default settings will be used</param>
    /// <returns>The deserialized object of type T.</returns>
    public static T? Deserialize<T>(string json, JsonSerializerSettings? serializerSettings){
        try{
            return JsonConvert.DeserializeObject<T>(json, serializerSettings);
        } catch (JsonException ex){
            Console.Error.WriteLine($"Error deserializing JSON: {ex.Message}");
            throw;
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

        for (int i = 0; i < chapterLines.Length; i += 2){
            var timeLine = chapterLines[i];
            var nameLine = chapterLines[i + 1];

            var timeParts = timeLine.Split('=');
            var nameParts = nameLine.Split('=');

            if (timeParts.Length == 2 && nameParts.Length == 2){
                var startTime = TimeSpan.Parse(timeParts[1]).TotalMilliseconds;
                var endTime = i + 2 < chapterLines.Length ? TimeSpan.Parse(chapterLines[i + 2].Split('=')[1]).TotalMilliseconds : startTime + 10000;

                ffmpegChapterLines.Add("[CHAPTER]");
                ffmpegChapterLines.Add("TIMEBASE=1/1000");
                ffmpegChapterLines.Add($"START={startTime}");
                ffmpegChapterLines.Add($"END={endTime}");
                ffmpegChapterLines.Add($"title={nameParts[1]}");
            }
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
                        Console.WriteLine($"{e.Data}");
                    }
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                // Define success condition more appropriately based on the application
                bool isSuccess = process.ExitCode == 0;

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
        // Define the regular expression pattern to match |S followed by a number and optionally C followed by another number
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
}