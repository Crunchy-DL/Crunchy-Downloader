using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CRD.Downloader;
using CRD.Utils.Structs;
using Newtonsoft.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
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

    public static readonly string PathLogFile = WorkingDirectory + "/logfile.txt";

    private static StreamWriter logFile;
    private static bool isLogModeEnabled = false;

    public static void EnableLogMode(){
        if (!isLogModeEnabled){
            logFile = new StreamWriter(PathLogFile);
            logFile.AutoFlush = true;
            Console.SetError(logFile);
            isLogModeEnabled = true;
            Console.Error.WriteLine("Log mode enabled.");
        }
    }

    public static void DisableLogMode(){
        if (isLogModeEnabled){
            logFile.Close();
            StreamWriter standardError = new StreamWriter(Console.OpenStandardError());
            standardError.AutoFlush = true;
            Console.SetError(standardError);
            isLogModeEnabled = false;
            Console.Error.WriteLine("Log mode disabled.");
        }
    }

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
            .IgnoreUnmatchedProperties()
            .Build();

        var propertiesPresentInYaml = GetTopLevelPropertiesInYaml(input);
        var loadedOptions = deserializer.Deserialize<CrDownloadOptions>(new StringReader(input));
        var instanceOptions = Crunchyroll.Instance.CrunOptions;

        foreach (PropertyInfo property in typeof(CrDownloadOptions).GetProperties()){
            var yamlMemberAttribute = property.GetCustomAttribute<YamlMemberAttribute>();
            string yamlPropertyName = yamlMemberAttribute?.Alias ?? property.Name;

            if (propertiesPresentInYaml.Contains(yamlPropertyName)){
                PropertyInfo instanceProperty = instanceOptions.GetType().GetProperty(property.Name);
                if (instanceProperty != null && instanceProperty.CanWrite){
                    instanceProperty.SetValue(instanceOptions, property.GetValue(loadedOptions));
                }
            }
        }
    }

    private static HashSet<string> GetTopLevelPropertiesInYaml(string yamlContent){
        var reader = new StringReader(yamlContent);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        var properties = new HashSet<string>();

        if (yamlStream.Documents.Count > 0 && yamlStream.Documents[0].RootNode is YamlMappingNode rootNode){
            foreach (var entry in rootNode.Children){
                if (entry.Key is YamlScalarNode scalarKey){
                    properties.Add(scalarKey.Value);
                }
            }
        }

        return properties;
    }

    private static object fileLock = new object();

    public static void WriteJsonToFile(string pathToFile, object obj){
        try{
            // Check if the directory exists; if not, create it.
            string directoryPath = Path.GetDirectoryName(pathToFile);
            if (!Directory.Exists(directoryPath)){
                Directory.CreateDirectory(directoryPath);
            }

            lock (fileLock){
                // Write the JSON string to file using a streaming approach.
                using (var fileStream = new FileStream(pathToFile, FileMode.Create, FileAccess.Write))
                using (var streamWriter = new StreamWriter(fileStream))
                using (var jsonWriter = new JsonTextWriter(streamWriter){ Formatting = Formatting.Indented }){
                    var serializer = new JsonSerializer();
                    serializer.Serialize(jsonWriter, obj);
                }
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
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