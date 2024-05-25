using System;
using System.IO;
using CRD.Downloader;
using CRD.Utils.Structs;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CRD.Utils;

public class CfgManager{
    private static string WorkingDirectory = Directory.GetCurrentDirectory();

    public static readonly string PathCrToken = WorkingDirectory + "/config/cr_token.yml";
    public static readonly string PathCrDownloadOptions = WorkingDirectory + "/config/settings.yml";
    public static readonly string PathCrHistory = WorkingDirectory + "/config/history.json";

    public static readonly string PathFFMPEG = WorkingDirectory + "/lib/ffmpeg.exe";
    public static readonly string PathMKVMERGE = WorkingDirectory + "/lib/mkvmerge.exe";
    public static readonly string PathMP4Decrypt = WorkingDirectory + "/lib/mp4decrypt.exe";

    public static readonly string PathWIDEVINE_DIR = WorkingDirectory + "/widevine/";

    public static readonly string PathVIDEOS_DIR = WorkingDirectory + "/video/";
    public static readonly string PathFONTS_DIR = WorkingDirectory + "/video/";


    public static void WriteJsonResponseToYamlFile(string jsonResponse, string filePath){
        // Convert JSON to an object
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance) // Adjust this as needed
            .Build();
        var jsonObject = deserializer.Deserialize<object>(jsonResponse);

        // Convert the object to YAML
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance) // Ensure consistent naming convention
            .Build();
        var yaml = serializer.Serialize(jsonObject);

        string dirPath = Path.GetDirectoryName(filePath) ?? string.Empty;

        if (!Directory.Exists(dirPath)){
            Directory.CreateDirectory(dirPath);
        }

        if (!File.Exists(filePath)){
            using (var fileStream = File.Create(filePath)){
            }
        }

        // Write the YAML to a file
        File.WriteAllText(filePath, yaml);
    }

    public static void WriteTokenToYamlFile(CrToken token, string filePath){
        // Convert the object to YAML
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance) // Ensure consistent naming convention
            .Build();
        var yaml = serializer.Serialize(token);

        string dirPath = Path.GetDirectoryName(filePath) ?? string.Empty;

        if (!Directory.Exists(dirPath)){
            Directory.CreateDirectory(dirPath);
        }

        if (!File.Exists(filePath)){
            using (var fileStream = File.Create(filePath)){
            }
        }

        // Write the YAML to a file
        File.WriteAllText(filePath, yaml);
    }

    public static void WriteSettingsToFile(){
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance) // Use the underscore style
            .Build();

        string dirPath = Path.GetDirectoryName(PathCrDownloadOptions) ?? string.Empty;

        if (!Directory.Exists(dirPath)){
            Directory.CreateDirectory(dirPath);
        }

        if (!File.Exists(PathCrDownloadOptions)){
            using (var fileStream = File.Create(PathCrDownloadOptions)){
            }
        }

        var yaml = serializer.Serialize(Crunchyroll.Instance.CrunOptions);

        // Write to file
        File.WriteAllText(PathCrDownloadOptions, yaml);
    }

    public static void UpdateSettingsFromFile(){
        string dirPath = Path.GetDirectoryName(PathCrDownloadOptions) ?? string.Empty;

        if (!Directory.Exists(dirPath)){
            Directory.CreateDirectory(dirPath);
        }

        if (!File.Exists(PathCrDownloadOptions)){
            using (var fileStream = File.Create(PathCrDownloadOptions)){
            }

            return;
        }

        var input = File.ReadAllText(PathCrDownloadOptions);

        if (input.Length <= 0){
            return;
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties() // Important to ignore properties not present in YAML
            .Build();

        var loadedOptions = deserializer.Deserialize<CrDownloadOptions>(new StringReader(input));

        Crunchyroll.Instance.CrunOptions.Hslang = loadedOptions.Hslang;
        Crunchyroll.Instance.CrunOptions.Novids = loadedOptions.Novids;
        Crunchyroll.Instance.CrunOptions.Noaudio = loadedOptions.Noaudio;
        Crunchyroll.Instance.CrunOptions.FileName = loadedOptions.FileName;
        Crunchyroll.Instance.CrunOptions.Numbers = loadedOptions.Numbers;
        Crunchyroll.Instance.CrunOptions.DlSubs = loadedOptions.DlSubs;
        Crunchyroll.Instance.CrunOptions.Mp4 = loadedOptions.Mp4;
        Crunchyroll.Instance.CrunOptions.FfmpegOptions = loadedOptions.FfmpegOptions;
        Crunchyroll.Instance.CrunOptions.MkvmergeOptions = loadedOptions.MkvmergeOptions;
        Crunchyroll.Instance.CrunOptions.Chapters = loadedOptions.Chapters;
        Crunchyroll.Instance.CrunOptions.SimultaneousDownloads = loadedOptions.SimultaneousDownloads;
        Crunchyroll.Instance.CrunOptions.QualityAudio = loadedOptions.QualityAudio;
        Crunchyroll.Instance.CrunOptions.QualityVideo = loadedOptions.QualityVideo;
        Crunchyroll.Instance.CrunOptions.DubLang = loadedOptions.DubLang;
        Crunchyroll.Instance.CrunOptions.Theme = loadedOptions.Theme;
        Crunchyroll.Instance.CrunOptions.AccentColor = loadedOptions.AccentColor;
        Crunchyroll.Instance.CrunOptions.History = loadedOptions.History;
        Crunchyroll.Instance.CrunOptions.UseNonDrmStreams = loadedOptions.UseNonDrmStreams;
        Crunchyroll.Instance.CrunOptions.SonarrProperties = loadedOptions.SonarrProperties;
    }

    private static object fileLock = new object();

    public static void WriteJsonToFile(string pathToFile, object obj){
        try{
            // Serialize the object to a JSON string.
            var jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented);

            // Check if the directory exists; if not, create it.
            string directoryPath = Path.GetDirectoryName(pathToFile);
            if (!Directory.Exists(directoryPath)){
                Directory.CreateDirectory(directoryPath);
            }

            lock (fileLock){
                // Write the JSON string to file. Creates the file if it does not exist.
                File.WriteAllText(pathToFile, jsonString);
            }
        } catch (Exception ex){
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
    
    public static bool CheckIfFileExists(string filePath){
        string dirPath = Path.GetDirectoryName(filePath) ?? string.Empty;

        return Directory.Exists(dirPath) && File.Exists(filePath);
    }

    public static T DeserializeFromFile<T>(string filePath){
        var deserializer = new DeserializerBuilder()
            .Build();

        using (var reader = new StreamReader(filePath)){
            return deserializer.Deserialize<T>(reader);
        }
    }
}