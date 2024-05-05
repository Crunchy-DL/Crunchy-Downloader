using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CRD.Utils.Updater;

public class Updater{
    #region Singelton

    private static Updater? _instance;
    private static readonly object Padlock = new();

    public static Updater Instance{
        get{
            if (_instance == null){
                lock (Padlock){
                    if (_instance == null){
                        _instance = new Updater();
                    }
                }
            }

            return _instance;
        }
    }

    #endregion

    private string downloadUrl = "";
    private readonly string tempPath = Path.Combine(Path.GetTempPath(), "Update.zip");
    private readonly string extractPath = Path.Combine(Path.GetTempPath(), "ExtractedUpdate");

    private readonly string apiEndpoint = "https://api.github.com/repos/Crunchy-DL/Crunchy-Downloader/releases/latest";

    public async Task<bool> CheckForUpdatesAsync(){
        try{
            using (var client = new HttpClient()){
                client.DefaultRequestHeaders.Add("User-Agent", "C# App"); // GitHub API requires a user agent
                var response = await client.GetStringAsync(apiEndpoint);
                var releaseInfo = JsonConvert.DeserializeObject<dynamic>(response);

                var latestVersion = releaseInfo.tag_name;
                downloadUrl = releaseInfo.assets[0].browser_download_url;

                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var currentVersion = $"v{version?.Major}.{version?.Minor}.{version?.Build}";


                if (latestVersion != currentVersion){
                    Console.WriteLine("Update available: " + latestVersion + " - Current Version: " + currentVersion);
                    return true;
                } else{
                    Console.WriteLine("No updates available.");
                    return false;
                }
            }
        } catch (Exception e){
            Console.WriteLine("Failed to get Update information");
            return false;
        }
    }

    public async Task DownloadAndUpdateAsync(){
        try{
            using (var client = new HttpClient()){
                // Download the zip file
                var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None)){
                    await stream.CopyToAsync(fileStream);
                }

                ZipFile.ExtractToDirectory(tempPath, extractPath, true);

                ApplyUpdate(extractPath);
            }
        } catch (Exception e){
            Console.WriteLine("Failed to get Update");
        }
    }

    private void ApplyUpdate(string updateFolder){
        var currentPath = AppDomain.CurrentDomain.BaseDirectory;
        var updaterPath = Path.Combine(currentPath, "Updater.exe");
        var arguments = $"\"{currentPath.Substring(0, currentPath.Length - 1)}\" \"{updateFolder}\"";

        System.Diagnostics.Process.Start(updaterPath, arguments);
        Environment.Exit(0);
    }
}