using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using CRD.Downloader.Crunchyroll;
using Newtonsoft.Json;

namespace CRD.Utils.Files;

public class CfgManager{
    private static string workingDirectory = AppContext.BaseDirectory;
    
    public static readonly string PathCrToken = Path.Combine(workingDirectory, "config", "cr_token.json");
    public static readonly string PathCrDownloadOptions = Path.Combine(workingDirectory, "config", "settings.json");

    public static readonly string PathCrHistory = Path.Combine(workingDirectory, "config", "history.json");
    public static readonly string PathWindowSettings = Path.Combine(workingDirectory, "config", "windowSettings.json");

    private static readonly string ExecutableExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

    public static readonly string PathFFMPEG = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(workingDirectory, "lib", "ffmpeg.exe") :
        File.Exists(Path.Combine(workingDirectory, "lib", "ffmpeg")) ? Path.Combine(workingDirectory, "lib", "ffmpeg") : "ffmpeg";

    public static readonly string PathMKVMERGE = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(workingDirectory, "lib", "mkvmerge.exe") :
        File.Exists(Path.Combine(workingDirectory, "lib", "mkvmerge")) ? Path.Combine(workingDirectory, "lib", "mkvmerge") : "mkvmerge";

    public static readonly string PathMP4Decrypt = Path.Combine(workingDirectory, "lib", "mp4decrypt" + ExecutableExtension);
    public static readonly string PathShakaPackager = Path.Combine(workingDirectory, "lib", "shaka-packager" + ExecutableExtension);

    public static readonly string PathWIDEVINE_DIR = Path.Combine(workingDirectory, "widevine");

    public static readonly string PathVIDEOS_DIR = Path.Combine(workingDirectory, "video");
    public static readonly string PathENCODING_PRESETS_DIR = Path.Combine(workingDirectory, "presets");
    public static readonly string PathTEMP_DIR = Path.Combine(workingDirectory, "temp");
    public static readonly string PathFONTS_DIR = Path.Combine(workingDirectory, "fonts");

    public static readonly string PathLogFile = Path.Combine(workingDirectory, "logfile.txt");

    private static StreamWriter logFile;
    private static bool isLogModeEnabled = false;

    static CfgManager(){
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static void OnProcessExit(object? sender, EventArgs e){
        DisableLogMode();
    }

    public static void EnableLogMode(){
        if (!isLogModeEnabled){
            try{
                var fileStream = new FileStream(PathLogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                logFile = new StreamWriter(fileStream);
                logFile.AutoFlush = true;
                Console.SetError(logFile);
                isLogModeEnabled = true;
                Console.Error.WriteLine("Log mode enabled.");
            } catch (Exception e){
                Console.Error.WriteLine($"Couldn't enable logging: {e}");
            }
        }
    }

    public static void DisableLogMode(){
        if (isLogModeEnabled){
            try{
                logFile.Close();
                StreamWriter standardError = new StreamWriter(Console.OpenStandardError());
                standardError.AutoFlush = true;
                Console.SetError(standardError);
                isLogModeEnabled = false;
                Console.Error.WriteLine("Log mode disabled.");
            } catch (Exception e){
                Console.Error.WriteLine($"Couldn't disable logging: {e}");
            }
        }
    }

    public static void WriteCrSettings(){
        WriteJsonToFile(PathCrDownloadOptions, CrunchyrollManager.Instance.CrunOptions);
    }

    // public static void WriteTokenToYamlFile(CrToken token, string filePath){
    //     // Convert the object to YAML
    //     var serializer = new SerializerBuilder()
    //         .WithNamingConvention(UnderscoredNamingConvention.Instance) // Ensure consistent naming convention
    //         .Build();
    //     var yaml = serializer.Serialize(token);
    //
    //     string dirPath = Path.GetDirectoryName(filePath) ?? string.Empty;
    //
    //     if (!Directory.Exists(dirPath)){
    //         Directory.CreateDirectory(dirPath);
    //     }
    //
    //     if (!File.Exists(filePath)){
    //         using (var fileStream = File.Create(filePath)){
    //         }
    //     }
    //
    //     // Write the YAML to a file
    //     File.WriteAllText(filePath, yaml);
    // }

    public static void UpdateSettingsFromFile<T>(T options, string filePath) where T : class{
        if (options == null){
            throw new ArgumentNullException(nameof(options));
        }

        string dirPath = Path.GetDirectoryName(filePath) ?? string.Empty;

        if (!Directory.Exists(dirPath)){
            Directory.CreateDirectory(dirPath);
        }

        if (!File.Exists(filePath)){
            // Create the file if it doesn't exist
            using (var fileStream = File.Create(filePath)){
            }

            return;
        }

        var input = File.ReadAllText(filePath);

        if (string.IsNullOrWhiteSpace(input)){
            return;
        }

        // Deserialize JSON into a dictionary to get top-level properties
        var propertiesPresentInJson = GetTopLevelPropertiesInJson(input);

        // Deserialize JSON into the provided options object type
        var loadedOptions = JsonConvert.DeserializeObject<T>(input);

        if (loadedOptions == null){
            return;
        }

        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)){
            // Use the JSON property name if present, otherwise use the property name
            string jsonPropertyName = property.Name;
            var jsonPropertyAttribute = property.GetCustomAttribute<JsonPropertyAttribute>();
            if (jsonPropertyAttribute != null){
                jsonPropertyName = jsonPropertyAttribute.PropertyName ?? property.Name;
            }

            if (propertiesPresentInJson.Contains(jsonPropertyName)){
                // Update the target property
                var value = property.GetValue(loadedOptions);
                var targetProperty = options.GetType().GetProperty(property.Name);

                if (targetProperty != null && targetProperty.CanWrite){
                    targetProperty.SetValue(options, value);
                }
            }
        }
    }

    private static HashSet<string> GetTopLevelPropertiesInJson(string jsonContent){
        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var reader = new JsonTextReader(new StringReader(jsonContent))){
            while (reader.Read()){
                if (reader.TokenType == JsonToken.PropertyName){
                    properties.Add(reader.Value?.ToString() ?? string.Empty);
                }
            }
        }

        return properties;
    }

    public static void UpdateHistoryFile(){
        if (!CrunchyrollManager.Instance.CrunOptions.History){
            return;
        }

        WriteJsonToFileCompressed(PathCrHistory, CrunchyrollManager.Instance.HistoryList);
    }

    private static object fileLock = new object();

    public static void WriteJsonToFileCompressed(string pathToFile, object obj){
        try{
            // Check if the directory exists; if not, create it.
            string directoryPath = Path.GetDirectoryName(pathToFile);
            if (!Directory.Exists(directoryPath)){
                Directory.CreateDirectory(directoryPath);
            }

            lock (fileLock){
                using (var fileStream = new FileStream(pathToFile, FileMode.Create, FileAccess.Write))
                using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
                using (var streamWriter = new StreamWriter(gzipStream))
                using (var jsonWriter = new JsonTextWriter(streamWriter){ Formatting = Formatting.Indented }){
                    var serializer = new JsonSerializer();
                    serializer.Serialize(jsonWriter, obj);
                }
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public static void WriteJsonToFile(string pathToFile, object obj){
        try{
            // Check if the directory exists; if not, create it.
            string directoryPath = Path.GetDirectoryName(pathToFile);
            if (!Directory.Exists(directoryPath)){
                Directory.CreateDirectory(directoryPath);
            }

            lock (fileLock){
                using (var fileStream = new FileStream(pathToFile, FileMode.Create, FileAccess.Write, FileShare.None))
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

    public static string DecompressJsonFile(string pathToFile){
        try{
            using (var fileStream = new FileStream(pathToFile, FileMode.Open, FileAccess.Read)){
                // Check if the file is compressed
                if (IsFileCompressed(fileStream)){
                    // Reset the stream position to the beginning
                    fileStream.Position = 0;
                    using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                    using (var streamReader = new StreamReader(gzipStream)){
                        return streamReader.ReadToEnd();
                    }
                }

                // If not compressed, read the file as is
                fileStream.Position = 0;
                using (var streamReader = new StreamReader(fileStream)){
                    return streamReader.ReadToEnd();
                }
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
            return null;
        }
    }

    private static bool IsFileCompressed(FileStream fileStream){
        // Check the first two bytes for the GZip header
        var buffer = new byte[2];
        fileStream.Read(buffer, 0, 2);
        return buffer[0] == 0x1F && buffer[1] == 0x8B;
    }


    public static bool CheckIfFileExists(string filePath){
        string dirPath = Path.GetDirectoryName(filePath) ?? string.Empty;

        return Directory.Exists(dirPath) && File.Exists(filePath);
    }

    // public static T DeserializeFromFile<T>(string filePath){
    //     var deserializer = new DeserializerBuilder()
    //         .Build();
    //
    //     using (var reader = new StreamReader(filePath)){
    //         return deserializer.Deserialize<T>(reader);
    //     }
    // }

    public static T? ReadJsonFromFile<T>(string pathToFile) where T : class{
        try{
            if (!File.Exists(pathToFile)){
                throw new FileNotFoundException($"The file at path {pathToFile} does not exist.");
            }

            lock (fileLock){
                using (var fileStream = new FileStream(pathToFile, FileMode.Open, FileAccess.Read))
                using (var streamReader = new StreamReader(fileStream))
                using (var jsonReader = new JsonTextReader(streamReader)){
                    var serializer = new JsonSerializer();
                    return serializer.Deserialize<T>(jsonReader);
                }
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred while reading the JSON file: {ex.Message}");
            return null;
        }
    }
}