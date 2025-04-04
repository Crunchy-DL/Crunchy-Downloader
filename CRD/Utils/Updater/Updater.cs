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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CRD.Utils.Files;
using Newtonsoft.Json;

namespace CRD.Utils.Updater;

public class Updater : INotifyPropertyChanged{
    public double progress = 0;
    public bool failed = false;

    public string latestVersion = "";

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
    private readonly string changelogFilePath = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");

    private static readonly string apiEndpoint = "https://api.github.com/repos/Crunchy-DL/Crunchy-Downloader/releases";
    private static readonly string apiEndpointLatest = apiEndpoint + "/latest";

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
                var response = await client.GetStringAsync(apiEndpointLatest);
                var releaseInfo = Helpers.Deserialize<GithubRelease>(response, null);

                if (releaseInfo == null){
                    Console.WriteLine($"Failed to get Update info");
                    return false;
                }

                latestVersion = releaseInfo.TagName;

                if (releaseInfo.Assets != null)
                    foreach (var asset in releaseInfo.Assets){
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
                    _ = UpdateChangelogAsync();
                    return true;
                }

                Console.WriteLine("No updates available.");
                _ = UpdateChangelogAsync();
                return false;
            }
        } catch (Exception e){
            Console.Error.WriteLine("Failed to get Update information");
            return false;
        }
    }

    public async Task UpdateChangelogAsync(){
        var client = HttpClientReq.Instance.GetHttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "C# App");

        string existingVersion = GetLatestVersionFromFile();

        if (string.IsNullOrEmpty(existingVersion)){
            existingVersion = "v1.0.0";
        }
        
        if (string.IsNullOrEmpty(latestVersion)){
            latestVersion = "v1.0.0";
        }

        if (existingVersion == latestVersion || Version.Parse(existingVersion.TrimStart('v')) >= Version.Parse(latestVersion.TrimStart('v'))){
            Console.WriteLine("CHANGELOG.md is already up to date.");
            return;
        }

        try{
            string jsonResponse = await client.GetStringAsync(apiEndpoint); // + "?per_page=100&page=1"

            var releases = Helpers.Deserialize<List<GithubRelease>>(jsonResponse, null);

            // Filter out pre-releases
            if (releases != null){
                releases = releases.Where(r => !r.Prerelease).ToList();

                if (releases.Count == 0){
                    Console.WriteLine("No stable releases found.");
                    return;
                }

                var newReleases = releases.TakeWhile(r => r.TagName != existingVersion).ToList();

                if (newReleases.Count == 0){
                    Console.WriteLine("CHANGELOG.md is already up to date.");
                    return;
                }

                Console.WriteLine($"Adding {newReleases.Count} new releases to CHANGELOG.md...");

                AppendNewReleasesToChangelog(newReleases);

                Console.WriteLine("CHANGELOG.md updated successfully.");
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"Error updating changelog: {ex.Message}");
        }
    }

    private string GetLatestVersionFromFile(){
        if (!File.Exists(changelogFilePath))
            return string.Empty;

        string[] lines = File.ReadAllLines(changelogFilePath);
        foreach (string line in lines){
            Match match = Regex.Match(line, @"## \[(v?\d+\.\d+\.\d+)\]");
            if (match.Success)
                return match.Groups[1].Value;
        }

        return string.Empty;
    }

    private void AppendNewReleasesToChangelog(List<GithubRelease> newReleases){
        string existingContent = "";

        if (File.Exists(changelogFilePath)){
            existingContent = File.ReadAllText(changelogFilePath);
        }

        string newEntries = "";

        foreach (var release in newReleases){
            string version = release.TagName;
            string date = release.PublishedAt.Split('T')[0];
            string notes = RemoveUnwantedContent(release.Body);

            newEntries += $"## [{version}] - {date}\n\n{notes}\n\n---\n\n";
        }


        if (string.IsNullOrWhiteSpace(existingContent)){
            File.WriteAllText(changelogFilePath, "# Changelog\n\n" + newEntries);
        } else{
            File.WriteAllText(changelogFilePath, "# Changelog\n\n" + newEntries + existingContent.Substring("# Changelog\n\n".Length));
        }
    }

    private static string RemoveUnwantedContent(string notes){
        return Regex.Split(notes, @"##\r\n\r\n### Linux/MacOS Builds", RegexOptions.IgnoreCase)[0].Trim();
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

    public class GithubRelease{
        [JsonProperty("tag_name")]
        public string TagName{ get; set; } = string.Empty;

        public dynamic? Assets{ get; set; }
        public string Body{ get; set; } = string.Empty;

        [JsonProperty("published_at")]
        public string PublishedAt{ get; set; } = string.Empty;

        public bool Prerelease{ get; set; }
    }
}