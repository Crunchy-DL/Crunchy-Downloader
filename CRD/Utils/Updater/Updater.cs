using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CRD.Utils.Updater;

public class Updater : INotifyPropertyChanged{
    
    public double progress = 0;
    
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

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null){
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private string downloadUrl = "";
    private readonly string tempPath = Path.Combine(Path.GetTempPath(), "Update.zip");
    private readonly string extractPath = Path.Combine(Path.GetTempPath(), "ExtractedUpdate");

    private readonly string apiEndpoint = "https://api.github.com/repos/Crunchy-DL/Crunchy-Downloader/releases/latest";

    public async Task<bool> CheckForUpdatesAsync(){
        try{
            HttpClientHandler handler = new HttpClientHandler();
            handler.UseProxy = false;
            using (var client = new HttpClient(handler)){
                client.DefaultRequestHeaders.Add("User-Agent", "C# App");
                var response = await client.GetStringAsync(apiEndpoint);
                var releaseInfo = Helpers.Deserialize<dynamic>(response,null);

                var latestVersion = releaseInfo.tag_name;
                downloadUrl = releaseInfo.assets[0].browser_download_url;

                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var currentVersion = $"v{version?.Major}.{version?.Minor}.{version?.Build}";


                if (latestVersion != currentVersion){
                    Console.WriteLine("Update available: " + latestVersion + " - Current Version: " + currentVersion);
                    return true;
                }

                Console.WriteLine("No updates available.");
                return false;
            }
        } catch (Exception e){
            Console.Error.WriteLine("Failed to get Update information");
            return false;
        }
    }


    public async Task DownloadAndUpdateAsync(){
        try{
            using (var client = new HttpClient()){
                // Download the zip file
                var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode){
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var totalBytesRead = 0L;
                    var buffer = new byte[8192];
                    var isMoreToRead = true;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None)){
                        do{
                            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead == 0){
                                isMoreToRead = false;
                                progress = 100;
                                OnPropertyChanged(nameof(progress));
                                continue;
                            }

                            await fileStream.WriteAsync(buffer, 0, bytesRead);

                            totalBytesRead += bytesRead;
                            if (totalBytes != -1){
                                progress = (double)totalBytesRead / totalBytes * 100;
                                OnPropertyChanged(nameof(progress));
                            }
                        } while (isMoreToRead);
                    }

                    ZipFile.ExtractToDirectory(tempPath, extractPath, true);

                    ApplyUpdate(extractPath);
                } else{
                    Console.Error.WriteLine("Failed to get Update");
                }
            }
        } catch (Exception e){
            Console.Error.WriteLine($"Failed to get Update: {e.Message}");
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