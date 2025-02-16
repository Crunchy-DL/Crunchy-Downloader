using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CRD.Utils.Files;

namespace CRD.Utils.Updater;

public class Updater : INotifyPropertyChanged{
    public double progress = 0;
    public bool failed = false;

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
    private readonly string tempPath = Path.Combine(CfgManager.PathTEMP_DIR, "Update.zip");
    private readonly string extractPath = Path.Combine(CfgManager.PathTEMP_DIR, "ExtractedUpdate");

    private readonly string apiEndpoint = "https://api.github.com/repos/Crunchy-DL/Crunchy-Downloader/releases/latest";

    public async Task<bool> CheckForUpdatesAsync(){
        if (File.Exists(tempPath)){
            File.Delete(tempPath);
        }

        if (Directory.Exists(extractPath)){
            Directory.Delete(extractPath, true);
        }

        try{
            var platformAssetMapping = new Dictionary<OSPlatform, string>{
                { OSPlatform.Windows, "windows" },
                { OSPlatform.Linux, "linux" },
                { OSPlatform.OSX, "macos" }
            };

            //windows-x64 windows-arm64
            //linux-x64 linux-arm64
            //macos-x64 macos-arm64

            string platformName = platformAssetMapping.FirstOrDefault(p => RuntimeInformation.IsOSPlatform(p.Key)).Value;

            string architecture = RuntimeInformation.OSArchitecture switch{
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                _ => ""
            };

            platformName = $"{platformName}-{architecture}";

            Console.WriteLine($"Running on {platformName}");

            HttpClientHandler handler = new HttpClientHandler();
            handler.UseProxy = false;
            using (var client = new HttpClient(handler)){
                client.DefaultRequestHeaders.Add("User-Agent", "C# App");
                var response = await client.GetStringAsync(apiEndpoint);
                var releaseInfo = Helpers.Deserialize<dynamic>(response, null);

                var latestVersion = releaseInfo.tag_name;

                foreach (var asset in releaseInfo.assets){
                    string assetName = (string)asset.name;
                    if (assetName.Contains(platformName)){
                        downloadUrl = asset.browser_download_url;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl)){
                    Console.WriteLine($"Failed to get Update url for {platformName}");
                    return false;
                }

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
            failed = false;
            Helpers.EnsureDirectoriesExist(tempPath);

            // Download the zip file
            var response = await HttpClientReq.Instance.GetHttpClient().GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

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

                if (Directory.Exists(extractPath)){
                    Directory.Delete(extractPath, true);
                }

                ZipFile.ExtractToDirectory(tempPath, extractPath, true);

                ApplyUpdate(extractPath);
            } else{
                Console.Error.WriteLine("Failed to get Update");
                failed = true;
                OnPropertyChanged(nameof(failed));
            }
        } catch (Exception e){
            Console.Error.WriteLine($"Failed to get Update: {e.Message}");
            failed = true;
            OnPropertyChanged(nameof(failed));
        }
    }

    private void ApplyUpdate(string updateFolder){
        var executableExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        var currentPath = Path.GetFullPath(AppContext.BaseDirectory);
        var updaterPath = Path.Combine(currentPath, "Updater" + executableExtension);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
            try{
                var chmodProcess = new System.Diagnostics.ProcessStartInfo{
                    FileName = "/bin/bash",
                    Arguments = $"-c \"chmod +x '{updaterPath}'\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(chmodProcess)?.WaitForExit();
            } catch (Exception ex){
                Console.Error.WriteLine($"Error setting execute permissions: {ex.Message}");
                failed = true;
                OnPropertyChanged(nameof(failed));
                return;
            }
        }

        try{
            var startInfo = new System.Diagnostics.ProcessStartInfo{
                FileName = updaterPath,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(currentPath);
            startInfo.ArgumentList.Add(updateFolder);

            System.Diagnostics.Process.Start(startInfo);
            Environment.Exit(0);
        } catch (Exception ex){
            Console.Error.WriteLine($"Error launching updater: {ex.Message}");
            failed = true;
            OnPropertyChanged(nameof(failed));
        }
    }
}