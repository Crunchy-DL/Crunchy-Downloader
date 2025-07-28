using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CRD.Downloader;
using CRD.Utils.Parser.Utils;
using CRD.Utils.Structs;
using Newtonsoft.Json;

namespace CRD.Utils.HLS;

public class HlsDownloader{
    private Data _data = new();

    private CrunchyEpMeta _currentEpMeta;
    private bool _isVideo;
    private bool _isAudio;
    private bool _newDownloadMethode;

    public HlsDownloader(HlsOptions options, CrunchyEpMeta meta, bool isVideo, bool isAudio, bool newDownloadMethode){
        if (options == null || options.M3U8Json == null || options.M3U8Json.Segments == null){
            throw new Exception("Playlist is empty");
        }

        _currentEpMeta = meta;

        _isVideo = isVideo;
        _isAudio = isAudio;

        _newDownloadMethode = newDownloadMethode;

        if (options?.M3U8Json != null)
            _data = new Data{
                Parts = new PartsData{
                    First = options.M3U8Json.MediaSequence ?? 0,
                    Total = options.M3U8Json.Segments?.Count,
                    Completed = 0,
                },
                M3U8Json = options.M3U8Json,
                OutputFile = options.Output ?? "stream.ts",
                Threads = options.Threads ?? 5,
                Retries = options.Retries ?? 4,
                Offset = options.Offset ?? 0,
                BaseUrl = options.BaseUrl,
                SkipInit = options.SkipInit ?? false,
                Timeout = options.Timeout ?? 15 * 1000,
                CheckPartLength = true,
                IsResume = options.Offset.HasValue && options.Offset.Value > 0,
                BytesDownloaded = 0,
                WaitTime = options.FsRetryTime ?? 1000 * 5,
                Override = options.Override,
                DateStart = 0
            };
    }


    public async Task<(bool Ok, PartsData Parts)> Download(){
        string fn = _data.OutputFile ?? string.Empty;

        if (File.Exists(fn) && File.Exists($"{fn}.resume") && _data.Offset < 1){
            try{
                Console.WriteLine("Resume data found! Trying to resume...");
                string resumeFileContent = File.ReadAllText($"{fn}.resume");
                var resumeData = Helpers.Deserialize<ResumeData>(resumeFileContent, null);

                if (resumeData != null){
                    if (resumeData.Total == _data.M3U8Json?.Segments.Count &&
                        resumeData.Completed != resumeData.Total &&
                        !double.IsNaN(resumeData.Completed)){
                        Console.WriteLine("Resume data is ok!");
                        _data.Offset = resumeData.Completed;
                        _data.IsResume = true;
                    } else{
                        if (resumeData.Total == _data.M3U8Json?.Segments.Count &&
                            resumeData.Completed == resumeData.Total &&
                            !double.IsNaN(resumeData.Completed)){
                            Console.WriteLine("Already finished");
                            return (Ok: true, _data.Parts);
                        }

                        Console.WriteLine("Resume data is wrong!");
                        Console.WriteLine($"Resume: {{ total: {resumeData.Total}, dled: {resumeData.Completed} }}, " +
                                          $"Current: {{ total: {_data.M3U8Json?.Segments.Count} }}");
                    }
                } else{
                    Console.WriteLine("Resume data is wrong!");
                    Console.WriteLine($"Resume: {{ total: {resumeData?.Total}, dled: {resumeData?.Completed} }}, " +
                                      $"Current: {{ total: {_data.M3U8Json?.Segments.Count} }}");
                }
            } catch (Exception e){
                Console.Error.WriteLine("Resume failed, downloading will not be resumed!");
                Console.Error.WriteLine(e.Message);
            }
        }

        // Check if the file exists and it is not a resume download
        if (File.Exists(fn) && !_data.IsResume){
            string rwts = !string.IsNullOrEmpty(_data.Override) ? _data.Override : "Y";
            rwts = rwts.ToUpper(); // ?? "N"

            if (rwts.StartsWith("Y")){
                Console.WriteLine($"Deleting «{fn}»...");
                File.Delete(fn);
            } else if (rwts.StartsWith("C")){
                return (Ok: true, _data.Parts);
            } else{
                return (Ok: false, _data.Parts);
            }
        }

        // Show output filename based on whether it's a resume
        if (File.Exists(fn) && _data.IsResume){
            Console.WriteLine($"Adding content to «{fn}»...");
        } else{
            Console.WriteLine($"Saving stream to «{fn}»...");
        }


        if (_data.M3U8Json != null){
            List<dynamic> segments = _data.M3U8Json.Segments;

            // map has init uri outside is none init uri
            // Download init part
            if (segments[0].map != null && _data.Offset == 0 && !_data.SkipInit){
                Console.WriteLine("Download and save init part...");
                Segment initSeg = new Segment();
                initSeg.Uri = ObjectUtilities.GetMemberValue(segments[0].map, "uri");
                initSeg.Key = ObjectUtilities.GetMemberValue(segments[0].map, "key");
                initSeg.ByteRange = ObjectUtilities.GetMemberValue(segments[0].map, "byteRange");

                if (ObjectUtilities.GetMemberValue(segments[0], "key") != null){
                    initSeg.Key = segments[0].Key;
                }

                try{
                    var initDl = await DownloadPart(initSeg, 0, 0);
                    await File.WriteAllBytesAsync(fn, initDl);
                    await File.WriteAllTextAsync($"{fn}.resume", JsonConvert.SerializeObject(new{ completed = 0, total = segments.Count }));
                    Console.WriteLine("Init part downloaded.");
                } catch (Exception e){
                    Console.Error.WriteLine($"Part init download error:\n\t{e.Message}");
                    return (false, this._data.Parts);
                }
            } else if (segments[0].map != null && this._data.Offset == 0 && this._data.SkipInit){
                Console.WriteLine("Skipping init part can lead to broken video!");
            }

            // Resuming ...
            if (_data.Offset > 0){
                segments = segments.GetRange(_data.Offset, segments.Count - _data.Offset);
                Console.WriteLine($"Resuming download from part {_data.Offset + 1}...");
                _data.Parts.Completed = _data.Offset;
            }


            if (_newDownloadMethode){
                return await DownloadSegmentsBufferedResumeAsync(segments, fn);
            }

            for (int p = 0; p < Math.Ceiling((double)segments.Count / _data.Threads); p++){
                // Start time
                _data.DateStart = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                int offset = p * _data.Threads;
                int dlOffset = Math.Min(offset + _data.Threads, segments.Count);

                int errorCount = 0;
                Dictionary<string, Task> keyTasks = new Dictionary<string, Task>();
                Dictionary<int, Task<byte[]>> partTasks = new Dictionary<int, Task<byte[]>>();
                List<byte[]> results = new List<byte[]>(new byte[dlOffset - offset][]);

                // Download keys
                for (int px = offset; px < dlOffset; px++){
                    var curSegment = segments[px];
                    var key = ObjectUtilities.GetMemberValue(curSegment, "key");
                    if (key != null && !keyTasks.ContainsKey(key?.Uri) && !_data.Keys.ContainsKey(key?.Uri)){
                        keyTasks[curSegment.Key.Uri] = DownloadKey(curSegment.Key, px, _data.Offset);
                    }
                }

                try{
                    await Task.WhenAll(keyTasks.Values);
                } catch (Exception ex){
                    Console.Error.WriteLine($"Error downloading keys: {ex.Message}");
                    throw;
                }

                for (int px = offset; px < dlOffset && px < segments.Count; px++){
                    var segment = new Segment();
                    segment.Uri = ObjectUtilities.GetMemberValue(segments[px], "uri");
                    segment.Key = ObjectUtilities.GetMemberValue(segments[px], "key");
                    segment.ByteRange = ObjectUtilities.GetMemberValue(segments[px], "byteRange");
                    partTasks[px] = DownloadPart(segment, px, _data.Offset);
                }

                while (partTasks.Count > 0){
                    Task<byte[]> completedTask = await Task.WhenAny(partTasks.Values);
                    int completedIndex = -1;
                    foreach (var task in partTasks){
                        if (task.Value == completedTask){
                            completedIndex = task.Key;
                            break;
                        }
                    }

                    if (completedIndex != -1){
                        try{
                            byte[] result = await completedTask;
                            results[completedIndex - offset] = result;
                            partTasks.Remove(completedIndex);
                        } catch (Exception ex){
                            Console.Error.WriteLine($"Part {completedIndex + 1 + _data.Offset} download error:\n\t{ex.Message}");
                            partTasks.Remove(completedIndex);
                            errorCount++;
                        }
                    }
                }

                if (errorCount > 0){
                    Console.Error.WriteLine($"{errorCount} parts not downloaded");
                    return (false, _data.Parts);
                }

                foreach (var part in results){
                    int attempt = 0;
                    bool writeSuccess = false;

                    while (attempt < 3 && !writeSuccess){
                        try{
                            using (var stream = new FileStream(fn, FileMode.Append, FileAccess.Write, FileShare.None)){
                                await stream.WriteAsync(part, 0, part.Length);
                            }

                            writeSuccess = true;
                        } catch (Exception ex){
                            Console.Error.WriteLine(ex);
                            Console.Error.WriteLine($"Unable to write to file '{fn}' (Attempt {attempt + 1}/3)");
                            Console.WriteLine($"Waiting {Math.Round(_data.WaitTime / 1000.0)}s before retrying");
                            await Task.Delay(_data.WaitTime);
                            attempt++;
                        }
                    }

                    if (!writeSuccess){
                        Console.Error.WriteLine($"Unable to write content to '{fn}'.");
                        return (Ok: false, _data.Parts);
                    }
                }

                int totalSeg = _data.Parts.Total; // + _data.Offset
                int downloadedSeg = Math.Min(dlOffset, totalSeg);
                _data.Parts.Completed = downloadedSeg + _data.Offset; // 

                var dataLog = GetDownloadInfo(_data.DateStart, _data.Parts.Completed, totalSeg, _data.BytesDownloaded, _data.TotalBytes);
                _data.BytesDownloaded = 0;

                // Save resume data to file
                string resumeDataJson = JsonConvert.SerializeObject(new{ _data.Parts.Completed, Total = totalSeg });
                File.WriteAllText($"{fn}.resume", resumeDataJson);

                // Log progress
                Console.WriteLine($"{_data.Parts.Completed} of {totalSeg} parts downloaded [{dataLog.Percent}%] ({FormatTime(dataLog.Time)} | {dataLog.DownloadSpeed / 1000000.0:F2}Mb/s)");

                _currentEpMeta.DownloadProgress = new DownloadProgress(){
                    IsDownloading = true,
                    Percent = dataLog.Percent,
                    Time = dataLog.Time,
                    DownloadSpeed = dataLog.DownloadSpeed,
                    Doing = _isAudio ? "Downloading Audio" : (_isVideo ? "Downloading Video" : "")
                };

                if (!QueueManager.Instance.Queue.Contains(_currentEpMeta)){
                    if (!_currentEpMeta.DownloadProgress.Done){
                        foreach (var downloadItemDownloadedFile in _currentEpMeta.downloadedFiles){
                            try{
                                if (File.Exists(downloadItemDownloadedFile)){
                                    File.Delete(downloadItemDownloadedFile);
                                }
                            } catch (Exception e){
                                Console.Error.WriteLine(e.Message);
                            }
                        }
                    }

                    return (Ok: false, _data.Parts);
                }

                QueueManager.Instance.Queue.Refresh();

                while (_currentEpMeta.Paused){
                    await Task.Delay(500);
                    if (!QueueManager.Instance.Queue.Contains(_currentEpMeta)){
                        return (Ok: false, _data.Parts);
                    }
                }
            }
        }

        return (Ok: true, _data.Parts);
    }

    private static readonly object _resumeLock = new object();

    public async Task<(bool Ok, PartsData Parts)> DownloadSegmentsBufferedResumeAsync(List<dynamic> segments, string fn){
        var totalSeg = _data.Parts.Total;
        string sessionId = Path.GetFileNameWithoutExtension(fn);
        string tempDir = Path.Combine(Path.GetDirectoryName(fn), $"{sessionId}_temp");

        Directory.CreateDirectory(tempDir);

        string resumeFile = $"{fn}.new.resume";
        int downloadedParts = 0;
        int mergedParts = 0;

        if (File.Exists(resumeFile)){
            try{
                var resumeData = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(resumeFile));
                downloadedParts = (int?)resumeData?.DownloadedParts ?? 0;
                mergedParts = (int?)resumeData?.MergedParts ?? 0;
            } catch{
            }
        }

        if (downloadedParts > totalSeg) downloadedParts = totalSeg;
        if (mergedParts > downloadedParts) mergedParts = downloadedParts;

        var semaphore = new SemaphoreSlim(_data.Threads);
        var downloadTasks = new List<Task>();
        bool errorOccurred = false;

        var _lastUiUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        for (int i = 0; i < segments.Count; i++){
            if (File.Exists(Path.Combine(tempDir, $"part_{i:D6}.tmp")))
                continue;

            int index = i;
            await semaphore.WaitAsync();

            downloadTasks.Add(Task.Run(async () => {
                try{
                    var segment = new Segment{
                        Uri = ObjectUtilities.GetMemberValue(segments[index], "uri"),
                        Key = ObjectUtilities.GetMemberValue(segments[index], "key"),
                        ByteRange = ObjectUtilities.GetMemberValue(segments[index], "byteRange")
                    };

                    var data = await DownloadPart(segment, index, _data.Offset);

                    string tempFile = Path.Combine(tempDir, $"part_{index:D6}.tmp");
                    await File.WriteAllBytesAsync(tempFile, data);

                    int currentDownloaded = Directory.GetFiles(tempDir, "part_*.tmp").Length;
                    lock (_resumeLock){
                        File.WriteAllText(resumeFile, JsonConvert.SerializeObject(new{
                            DownloadedParts = currentDownloaded,
                            MergedParts = mergedParts,
                            Total = totalSeg
                        }));
                    }

                    if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - _lastUiUpdate > 500){
                        var dataLog = GetDownloadInfo(
                            _lastUiUpdate,
                            currentDownloaded,
                            totalSeg,
                            _data.BytesDownloaded,
                            _data.TotalBytes
                        );

                        _data.BytesDownloaded = 0;
                        _lastUiUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                        Console.WriteLine($"{currentDownloaded}/{totalSeg} [{dataLog.Percent}%] Speed: {dataLog.DownloadSpeed / 1000000.0:F2} MB/s ETA: {FormatTime(dataLog.Time)}");

                        _currentEpMeta.DownloadProgress = new DownloadProgress{
                            IsDownloading = true,
                            Percent = dataLog.Percent,
                            Time = dataLog.Time,
                            DownloadSpeed = dataLog.DownloadSpeed,
                            Doing = _isAudio ? "Downloading Audio" : (_isVideo ? "Downloading Video" : "")
                        };
                    }

                    if (!QueueManager.Instance.Queue.Contains(_currentEpMeta))
                        return;

                    QueueManager.Instance.Queue.Refresh();

                    while (_currentEpMeta.Paused){
                        await Task.Delay(500);
                        if (!QueueManager.Instance.Queue.Contains(_currentEpMeta))
                            return;
                    }
                } catch (Exception ex){
                    Console.Error.WriteLine($"Error downloading part {index}: {ex.Message}");
                    errorOccurred = true;
                } finally{
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(downloadTasks);

        if (errorOccurred)
            return (false, _data.Parts);

        using (var output = new FileStream(fn, FileMode.Append, FileAccess.Write, FileShare.None)){
            for (int i = mergedParts; i < segments.Count; i++){
                string tempFile = Path.Combine(tempDir, $"part_{i:D6}.tmp");
                if (!File.Exists(tempFile)){
                    Console.Error.WriteLine($"Missing temp file for part {i}, aborting merge.");
                    return (false, _data.Parts);
                }

                byte[] data = await File.ReadAllBytesAsync(tempFile);
                await output.WriteAsync(data, 0, data.Length);

                mergedParts++;

                File.WriteAllText(resumeFile, JsonConvert.SerializeObject(new{
                    DownloadedParts = totalSeg,
                    MergedParts = mergedParts,
                    Total = totalSeg
                }));

                var dataLog = GetDownloadInfo(_data.DateStart, mergedParts, totalSeg, _data.BytesDownloaded, _data.TotalBytes);
                Console.WriteLine($"{mergedParts}/{totalSeg} parts merged [{dataLog.Percent}%]");

                _currentEpMeta.DownloadProgress = new DownloadProgress{
                    IsDownloading = true,
                    Percent = dataLog.Percent,
                    Time = dataLog.Time,
                    DownloadSpeed = dataLog.DownloadSpeed,
                    Doing = _isAudio ? "Merging Audio" : (_isVideo ? "Merging Video" : "")
                };

                if (!QueueManager.Instance.Queue.Contains(_currentEpMeta))
                    return (false, _data.Parts);
                
            }
        }

        // Cleanup temp files
        Directory.Delete(tempDir, true);
        File.Delete(resumeFile);

        return (true, _data.Parts);
    }


    public static Info GetDownloadInfo(long dateStartUnix, int partsDownloaded, int partsTotal, long incrementalBytes, long totalDownloadedBytes){
        DateTime lastStart = DateTimeOffset.FromUnixTimeMilliseconds(dateStartUnix).UtcDateTime;
        double elapsedMs = (DateTime.UtcNow - lastStart).TotalMilliseconds;
        if (elapsedMs <= 0) elapsedMs = 1;

        double speed = incrementalBytes / (elapsedMs / 1000);
        if (speed < 1) speed = 1;

        int percent = (int)((double)partsDownloaded / partsTotal * 100);
        if (percent > 100) percent = 100;

        double etaSec = 0;
        if (partsDownloaded > 0){
            double avgPartSize = (double)totalDownloadedBytes / partsDownloaded;
            double remainingBytes = avgPartSize * (partsTotal - partsDownloaded);
            etaSec = remainingBytes / speed;
        }

        if (etaSec > TimeSpan.MaxValue.TotalSeconds)
            etaSec = TimeSpan.MaxValue.TotalSeconds;

        return new Info{
            Percent = percent,
            Time = etaSec,
            DownloadSpeed = speed
        };
    }

    private string FormatTime(double seconds){
        TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
        return timeSpan.ToString(@"hh\:mm\:ss");
    }

    public async Task<byte[]> DownloadPart(Segment seg, int segIndex, int segOffset){
        string sUri = GetUri(seg.Uri ?? "", _data.BaseUrl);
        byte[]? dec = null;
        int p = segIndex;
        try{
            byte[]? part;
            if (seg.Key != null){
                var decipher = await GetKey(seg.Key, p, segOffset);
                part = await GetData(p, sUri, seg.ByteRange != null ? seg.ByteRange.ToDictionary() : new Dictionary<string, string>(), segOffset, false, _data.Timeout, _data.Retries);
                var partContent = part;
                using (decipher){
                    if (partContent != null) dec = decipher.TransformFinalBlock(partContent, 0, partContent.Length);
                }

                if (dec != null){
                    Interlocked.Add(ref _data.BytesDownloaded, dec.Length);
                    Interlocked.Add(ref _data.TotalBytes, dec.Length);
                }
            } else{
                part = await GetData(p, sUri, seg.ByteRange != null ? seg.ByteRange.ToDictionary() : new Dictionary<string, string>(), segOffset, false, _data.Timeout, _data.Retries);
                dec = part;
                if (dec != null){
                    Interlocked.Add(ref _data.BytesDownloaded, dec.Length);
                    Interlocked.Add(ref _data.TotalBytes, dec.Length);
                }
            }
        } catch (Exception ex){
            throw new Exception($"Error at segment {p}: {ex.Message}", ex);
        }

        return dec ?? Array.Empty<byte>();
    }

    private async Task<ICryptoTransform> GetKey(Key key, int segIndex, int segOffset){
        string kUri = GetUri(key.Uri ?? "", _data.BaseUrl);
        int p = segIndex;
        if (!_data.Keys.ContainsKey(kUri)){
            try{
                var rkey = await DownloadKey(key, segIndex, segOffset);
                if (rkey == null)
                    throw new Exception("Failed to download key");
                _data.Keys[kUri] = rkey;
            } catch (Exception ex){
                throw new Exception($"Key Error at segment {p}: {ex.Message}", ex);
            }
        }

        byte[] iv = new byte[16];
        var ivs = key.Iv; //?? new List<int>{ 0, 0, 0, p + 1 }
        for (int i = 0; i < ivs.Count; i++){
            byte[] bytes = BitConverter.GetBytes(ivs[i]);

            // Ensure the bytes are in big-endian order
            if (BitConverter.IsLittleEndian){
                Array.Reverse(bytes);
            }

            bytes.CopyTo(iv, i * 4);
        }

        ICryptoTransform decryptor;
        using (Aes aes = Aes.Create()){
            aes.Key = _data.Keys[kUri];
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            decryptor = aes.CreateDecryptor();
        }

        // var decryptor = new AesCryptoServiceProvider{
        //     Key = _data.Keys[kUri],
        //     IV = iv,
        //     Mode = CipherMode.CBC,
        //     Padding = PaddingMode.PKCS7
        // }.CreateDecryptor();
        return decryptor;
    }

    public async Task<byte[]> DownloadKey(Key key, int segIndex, int segOffset){
        string kUri = GetUri(key.Uri ?? "", _data.BaseUrl);
        if (!_data.Keys.ContainsKey(kUri)){
            try{
                var rkey = await GetData(segIndex, kUri, new Dictionary<string, string>(), segOffset, true, _data.Timeout, _data.Retries);
                if (rkey == null || rkey.Length != 16){
                    throw new Exception("Key not fully downloaded or is incorrect.");
                }

                _data.Keys[kUri] = rkey;
                return rkey;
            } catch (Exception ex){
                ex.Data["SegmentIndex"] = segIndex; // Adding custom data to the exception
                throw;
            }
        }

        return _data.Keys[kUri];
    }

    public async Task<byte[]?> GetData(int partIndex, string uri, IDictionary<string, string> headers, int segOffset, bool isKey, int timeout, int retryCount){
        // Handle local file URI
        if (uri.StartsWith("file://")){
            string path = new Uri(uri).LocalPath;
            return File.ReadAllBytes(path);
        }

        // Setup request headers
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        foreach (var header in headers){
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Set default user-agent if not provided
        if (!request.Headers.Contains("User-Agent")){
            request.Headers.Add("User-Agent", ApiUrls.FirefoxUserAgent);
        }

        return await SendRequestWithRetry(request, partIndex, segOffset, isKey, retryCount);
    }

    private async Task<byte[]?> SendRequestWithRetry(HttpRequestMessage requestPara, int partIndex, int segOffset, bool isKey, int retryCount){
        HttpResponseMessage response;
        for (int attempt = 0; attempt < retryCount + 1; attempt++){
            using (var request = CloneHttpRequestMessage(requestPara)){
                try{
                    response = await HttpClientReq.Instance.GetHttpClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    return await ReadContentAsByteArrayAsync(response.Content);
                } catch (Exception ex) when (ex is HttpRequestException or IOException){
                    // Log retry attempts
                    string partType = isKey ? "Key" : "Part";
                    int partIndx = partIndex + 1 + segOffset;
                    Console.Error.WriteLine($"{partType} {partIndx}: Attempt {attempt + 1} to retrieve data failed.");
                    Console.Error.WriteLine($"\tError: {ex.Message}");
                    if (attempt == retryCount)
                        throw; // rethrow after last retry

                    await Task.Delay(_data.WaitTime);
                } catch (Exception ex){
                    Console.Error.WriteLine($"Unexpected exception at part {partIndex + 1 + segOffset}:");
                    Console.Error.WriteLine($"\tType: {ex.GetType()}");
                    Console.Error.WriteLine($"\tMessage: {ex.Message}");
                    throw;
                }
            }
        }

        return null; // Should not reach here
    }

    private async Task<byte[]> ReadContentAsByteArrayAsync(HttpContent content){
        using (var memoryStream = new MemoryStream())
        using (var contentStream = await content.ReadAsStreamAsync())
        using (var throttledStream = new ThrottledStream(contentStream)){
            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await throttledStream.ReadAsync(buffer, 0, buffer.Length)) > 0){
                await memoryStream.WriteAsync(buffer, 0, bytesRead);
            }

            return memoryStream.ToArray();
        }
    }

    private HttpRequestMessage CloneHttpRequestMessage(HttpRequestMessage originalRequest){
        var clone = new HttpRequestMessage(originalRequest.Method, originalRequest.RequestUri){
            Content = originalRequest.Content?.Clone(),
            Version = originalRequest.Version
        };
        foreach (var header in originalRequest.Headers){
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var property in originalRequest.Properties){
            clone.Properties.Add(property);
        }

        return clone;
    }


    private static string GetUri(string uri, string? baseUrl = null){
        bool httpUri = Regex.IsMatch(uri, @"^https?:", RegexOptions.IgnoreCase);
        if (string.IsNullOrEmpty(baseUrl) && !httpUri){
            throw new ArgumentException("No base and not http(s) uri");
        } else if (httpUri){
            return uri;
        }

        return baseUrl + uri;
    }
}

public static class HttpContentExtensions{
    public static HttpContent Clone(this HttpContent content){
        if (content == null) return null;
        var memStream = new MemoryStream();
        content.CopyToAsync(memStream).Wait();
        memStream.Position = 0;
        var newContent = new StreamContent(memStream);
        foreach (var header in content.Headers){
            newContent.Headers.Add(header.Key, header.Value);
        }

        return newContent;
    }
}

public class Info{
    public int Percent{ get; set; }
    public double Time{ get; set; } // Remaining time estimate
    public double DownloadSpeed{ get; set; } // Bytes per second
}

public class ResumeData{
    public int Total{ get; set; }
    public int Completed{ get; set; }
}

public class M3U8Json{
    public dynamic Segments{ get; set; } = new List<dynamic>();
    public int? MediaSequence{ get; set; }
}

public class Segment{
    public string? Uri{ get; set; }
    public Key? Key{ get; set; }
    public ByteRange? ByteRange{ get; set; }
}

public class Key{
    public string? Uri{ get; set; }
    public List<int> Iv{ get; set; } = new List<int>();
}

public class ByteRange{
    public long Offset{ get; set; }
    public long Length{ get; set; }

    public IDictionary<string, string> ToDictionary(){
        return new Dictionary<string, string>{
            { "Offset", Offset.ToString() },
            { "Length", Length.ToString() }
        };
    }
}

public class HlsOptions{
    public M3U8Json? M3U8Json{ get; set; }
    public string? Output{ get; set; }
    public int? Threads{ get; set; }
    public int? Retries{ get; set; }
    public int? Offset{ get; set; }
    public string? BaseUrl{ get; set; }
    public bool? SkipInit{ get; set; }
    public int? Timeout{ get; set; }
    public int? FsRetryTime{ get; set; }
    public string? Override{ get; set; }
}

public class Data{
    public PartsData Parts{ get; set; } = new PartsData();
    public M3U8Json? M3U8Json{ get; set; }
    public string? OutputFile{ get; set; }
    public int Threads{ get; set; }
    public int Retries{ get; set; }
    public int Offset{ get; set; }
    public string? BaseUrl{ get; set; }
    public bool SkipInit{ get; set; }
    public Dictionary<string, byte[]> Keys{ get; set; } = new Dictionary<string, byte[]>(); // Object can be Buffer or string
    public int Timeout{ get; set; }
    public bool CheckPartLength{ get; set; }
    public bool IsResume{ get; set; }
    public int WaitTime{ get; set; }
    public string? Override{ get; set; }
    public long DateStart{ get; set; }

    public long BytesDownloaded;
    public long TotalBytes;
}

public class PartsData{
    public int First{ get; set; }
    public int Total{ get; set; }
    public int Completed{ get; set; }
}