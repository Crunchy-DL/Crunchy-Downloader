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
using CommunityToolkit.Mvvm.ComponentModel;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Files;
using CRD.Utils.Http;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace CRD.Utils.Updater;

public class Updater : ObservableObject{
    public double Progress;
    public bool Failed;
    public string LatestVersion = "";
    public List<GithubJson> GhAuthJson = [];

    public static Updater Instance{ get; } = new();

    private string downloadUrl = "";
    private readonly string tempPath = Path.Combine(CfgManager.PathTEMP_DIR, "Update", "Update.zip");
    private readonly string extractPath = Path.Combine(CfgManager.PathTEMP_DIR, "Update", "ExtractedUpdate");
    private readonly string changelogFilePath = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");

    private static readonly string ApiEndpoint = "https://api.github.com/repos/Crunchy-DL/Crunchy-Downloader/releases";
    private static readonly string ApiEndpointLatest = ApiEndpoint + "/latest";

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

            var infoVersion = Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                .Split('+')[0];

            var currentVersion = NuGetVersion.Parse(infoVersion ?? "0.0.0");

            HttpClientHandler handler = new HttpClientHandler();
            handler.UseProxy = false;
            using (var client = new HttpClient(handler)){
                client.DefaultRequestHeaders.Add("User-Agent", "C# App");
                var response = await client.GetStringAsync(ApiEndpoint);
                var releases = Helpers.Deserialize<List<GithubRelease>>(response, null) ?? [];

                bool allowPrereleases = CrunchyrollManager.Instance.CrunOptions.GhUpdatePrereleases;

                var selectedRelease = releases
                    .FirstOrDefault(r => allowPrereleases || !r.Prerelease);


                if (selectedRelease == null){
                    Console.WriteLine("No valid releases found.");
                    return false;
                }

                LatestVersion = selectedRelease.TagName;

                var latestVersion = NuGetVersion.Parse(selectedRelease.TagName.TrimStart('v'));

                if (latestVersion > currentVersion){
                    Console.WriteLine($"Update available: {LatestVersion} - Current Version: {currentVersion}");

                    var asset = selectedRelease.Assets?
                        .FirstOrDefault(a => a.IsForPlatform(platformName));

                    if (asset == null){
                        Console.WriteLine($"Failed to get Update url for {platformName}");
                        return false;
                    }

                    downloadUrl = asset.BrowserDownloadUrl;

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
    
    public async Task CheckGhJsonAsync(){
        var url = "https://Crunchy-DL.github.io/Crunchy-Downloader/data.json";
        try{
            HttpClientHandler handler = new HttpClientHandler();
            handler.UseProxy = false;
            
            using (var client = new HttpClient(handler)){
                client.DefaultRequestHeaders.Add("User-Agent", "C# App");
                var response = await client.GetStringAsync(url);
                var authList = Helpers.Deserialize<List<GithubJson>>(response, null);
                if (authList is{ Count: > 0 }){
                    GhAuthJson = authList;
                }
            }
        } catch (Exception e){
            Console.Error.WriteLine("Failed to get GH CR Auth information");
        }
    }

    public async Task UpdateChangelogAsync(){
        var client = HttpClientReq.Instance.GetHttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "C# App");

        string existingVersion = GetLatestVersionFromFile();

        if (string.IsNullOrEmpty(existingVersion)){
            existingVersion = "v1.0.0";
        }

        if (string.IsNullOrEmpty(LatestVersion)){
            LatestVersion = "v1.0.0";
        }

        if (!NuGetVersion.TryParse(existingVersion.TrimStart('v'), out var existingNuGetVersion)){
            existingNuGetVersion = NuGetVersion.Parse("1.0.0");
        }

        if (!NuGetVersion.TryParse(LatestVersion.TrimStart('v'), out var latestNuGetVersion)){
            latestNuGetVersion = NuGetVersion.Parse("1.0.0");
        }

        if (existingNuGetVersion >= latestNuGetVersion){
            Console.WriteLine("CHANGELOG.md is already up to date.");
            return;
        }

        try{
            string jsonResponse = await client.GetStringAsync(ApiEndpoint); // + "?per_page=100&page=1"

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
            Match match = Regex.Match(line, @"^## \[(v?[^\]]+)\]");
            if (!match.Success)
                continue;

            string versionText = match.Groups[1].Value;

            if (NuGetVersion.TryParse(versionText.TrimStart('v'), out _))
                return versionText;
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
            Failed = false;
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
                            Progress = 100;
                            OnPropertyChanged(nameof(Progress));
                            continue;
                        }

                        await fileStream.WriteAsync(buffer, 0, bytesRead);

                        totalBytesRead += bytesRead;
                        if (totalBytes != -1){
                            Progress = (double)totalBytesRead / totalBytes * 100;
                            OnPropertyChanged(nameof(Progress));
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
                Failed = true;
                OnPropertyChanged(nameof(Failed));
            }
        } catch (Exception e){
            Console.Error.WriteLine($"Failed to get Update: {e.Message}");
            Failed = true;
            OnPropertyChanged(nameof(Failed));
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
                Failed = true;
                OnPropertyChanged(nameof(Failed));
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
            Failed = true;
            OnPropertyChanged(nameof(Failed));
        }
    }
    
    public class GithubJson{
        [JsonProperty("type")]
        public string Type{ get; set; } = string.Empty;
        [JsonProperty("version_name")]
        public string VersionName{ get; set; } = string.Empty;
        [JsonProperty("version_code")]
        public string VersionCode{ get; set; } = string.Empty;
        [JsonProperty("Authorization")]
        public string Authorization{ get; set; } = string.Empty;

    }

    public class GithubRelease{
        [JsonProperty("tag_name")]
        public string TagName{ get; set; } = string.Empty;

        public List<GithubAsset>? Assets{ get; set; } = [];
        public string Body{ get; set; } = string.Empty;

        [JsonProperty("published_at")]
        public string PublishedAt{ get; set; } = string.Empty;

        public bool Prerelease{ get; set; }
    }

    public class GithubAsset{
        [JsonProperty("url")]
        public string Url{ get; set; } = "";

        [JsonProperty("id")]
        public long Id{ get; set; }

        [JsonProperty("node_id")]
        public string NodeId{ get; set; } = "";

        [JsonProperty("name")]
        public string Name{ get; set; } = "";

        [JsonProperty("label")]
        public string? Label{ get; set; }

        [JsonProperty("content_type")]
        public string ContentType{ get; set; } = "";

        [JsonProperty("state")]
        public string State{ get; set; } = "";

        [JsonProperty("size")]
        public long Size{ get; set; }

        [JsonProperty("digest")]
        public string? Digest{ get; set; }

        [JsonProperty("download_count")]
        public int DownloadCount{ get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt{ get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt{ get; set; }

        [JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl{ get; set; } = "";


        public bool IsForPlatform(string platform){
            return Name.Contains(platform, StringComparison.OrdinalIgnoreCase);
        }
    }
}