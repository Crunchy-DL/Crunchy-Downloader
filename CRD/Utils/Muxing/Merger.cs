using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using CRD.Utils.Files;
using CRD.Utils.Structs;

namespace CRD.Utils.Muxing;

public class Merger{
    public MergerOptions Options;

    public Merger(MergerOptions options){
        Options = options;

        if (Options.VideoTitle is{ Length: > 0 }){
            Options.VideoTitle = Options.VideoTitle.Replace("\"", "'");
        }
    }

    public string FFmpeg(){
        List<string> args = new List<string>();

        List<string> metaData = new List<string>();

        var index = 0;
        var audioIndex = 0;
        var hasVideo = false;

        args.Add("-loglevel warning");

        if (!Options.mp3){
            foreach (var vid in Options.OnlyVid){
                if (!hasVideo || Options.KeepAllVideos == true){
                    args.Add($"-i \"{vid.Path}\"");
                    metaData.Add($"-map {index}:v");
                    metaData.Add($"-metadata:s:v:{index} title=\"{(vid.Language.Name)}\"");
                    hasVideo = true;
                    index++;
                }
            }

            foreach (var aud in Options.OnlyAudio){
                if (aud.Delay != null && aud.Delay != 0){
                    double delay = aud.Delay / 1000.0 ?? 0;
                    args.Add($"-itsoffset {delay.ToString(CultureInfo.InvariantCulture)}");
                }

                args.Add($"-i \"{aud.Path}\"");
                metaData.Add($"-map {index}:a");
                metaData.Add($"-metadata:s:a:{audioIndex} language={aud.Language.Code}");
                if (Options.Defaults.Audio != null && Options.Defaults.Audio != Languages.DEFAULT_lang){
                    if (Options.Defaults.Audio.Code == aud.Language.Code){
                        metaData.Add($"-disposition:a:{audioIndex} default");
                    } else{
                        metaData.Add($"-disposition:a:{audioIndex} 0");
                    }
                } else{
                    metaData.Add($"-disposition:a:{audioIndex} 0");
                }

                index++;
                audioIndex++;
            }

            if (Options.Chapters != null && Options.Chapters.Count > 0){
                Helpers.ConvertChapterFileForFFMPEG(Options.Chapters[0].Path);

                args.Add($"-i \"{Options.Chapters[0].Path}\"");
                metaData.Add($"-map_metadata {index}");
                index++;
            }

            if (!Options.SkipSubMux){
                bool hasSignsSub = Options.Subtitles.Any(sub => sub.Signs && Options.Defaults.Sub?.Code == sub.Language.Code);

                foreach (var sub in Options.Subtitles.Select((value, i) => new{ value, i })){
                    if (sub.value.Delay != null && sub.value.Delay != 0){
                        double delay = sub.value.Delay / 1000.0 ?? 0;
                        args.Add($"-itsoffset {delay.ToString(CultureInfo.InvariantCulture)}");
                    }

                    args.Add($"-i \"{sub.value.File}\"");
                    metaData.Add($"-map {index}:s");
                    if (Options.Defaults.Sub != null && Options.Defaults.Sub != Languages.DEFAULT_lang){
                        if (Options.Defaults.Sub.Code == sub.value.Language.Code &&
                            (Options.DefaultSubSigns == sub.value.Signs || Options.DefaultSubSigns && !hasSignsSub)
                            && sub.value.ClosedCaption == false){
                            metaData.Add($"-disposition:s:{sub.i} default");
                        } else{
                            metaData.Add($"-disposition:s:{sub.i} 0");
                        }
                    } else{
                        metaData.Add($"-disposition:s:{sub.i} 0");
                    }

                    index++;
                }
            }


            args.AddRange(metaData);
            // args.AddRange(options.Subtitles.Select((sub, subIndex) => $"-map {subIndex + index}"));
            args.Add("-c:v copy");
            args.Add("-c:a copy");
            args.Add(Options.Output.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ? "-c:s mov_text" : "-c:s ass");
            if (!Options.SkipSubMux){
                args.AddRange(Options.Subtitles.Select((sub, subindex) =>
                    $"-metadata:s:s:{subindex} title=\"{sub.Language.Language ?? sub.Language.Name}{(sub.ClosedCaption == true ? $" {Options.CcTag}" : "")}{(sub.Signs == true ? " Signs" : "")}\" -metadata:s:s:{subindex} language={sub.Language.Code}"));
            }

            if (!string.IsNullOrEmpty(Options.VideoTitle)){
                args.Add($"-metadata title=\"{Options.VideoTitle}\"");
            }

            if (Options.Description is{ Count: > 0 }){
                XmlDocument doc = new XmlDocument();
                doc.Load(Options.Description[0].Path);
                XmlNode? node = doc.SelectSingleNode("//Tag/Simple[Name='DESCRIPTION']/String");
                string description = node?.InnerText
                                         .Replace("\\", "\\\\") // Escape backslashes
                                         .Replace("\"", "\\\"") // Escape double quotes
                                     ?? string.Empty;
                args.Add($"-metadata comment=\"{description}\"");
            }

            if (Options.Options.ffmpeg?.Count > 0){
                args.AddRange(Options.Options.ffmpeg);
            }

            args.Add($"\"{Options.Output}\"");

            return string.Join(" ", args);
        }


        if (Options.OnlyAudio.Count > 1){
            Console.Error.WriteLine("Multiple audio files detected. Only one audio file can be converted to MP3 at a time.");
        }

        var audio = Options.OnlyAudio.First();

        args.Add($"-i \"{audio.Path}\"");
        args.Add("-c:a libmp3lame" + (audio.Bitrate > 0 ? $" -b:a {audio.Bitrate}k" : ""));
        args.Add($"\"{Options.Output}\"");
        return string.Join(" ", args);
    }


    public string MkvMerge(){
        List<string> args = new List<string>();

        bool hasVideo = false;

        args.Add($"-o \"{Helpers.AddUncPrefixIfNeeded(Options.Output)}\"");
        if (Options.Options.mkvmerge != null){
            args.AddRange(Options.Options.mkvmerge);
        }


        foreach (var vid in Options.OnlyVid){
            if (!hasVideo || Options.KeepAllVideos == true){
                args.Add("--video-tracks 0");
                args.Add("--no-audio");

                string trackName = $"{(vid.Language.Name)}";
                args.Add($"--track-name 0:\"{trackName}\"");
                args.Add($"--language 0:{vid.Language.Code}");

                hasVideo = true;
                args.Add($"\"{Helpers.AddUncPrefixIfNeeded(vid.Path)}\"");
            }
        }

        // var sortedAudio = options.OnlyAudio
        //     .OrderBy(sub => options.DubLangList.IndexOf(sub.Language.CrLocale) != -1 ? options.DubLangList.IndexOf(sub.Language.CrLocale) : int.MaxValue)
        //     .ToList();

        var rank = Options.DubLangList
            .Select((val, i) => new{ val, i })
            .ToDictionary(x => x.val, x => x.i, StringComparer.OrdinalIgnoreCase);

        var sortedAudio = Options.OnlyAudio
            .OrderBy(m => {
                var key = m.Language?.CrLocale ?? string.Empty;
                return rank.TryGetValue(key, out var r) ? r : int.MaxValue; // unknown locales last
            })
            .ThenBy(m => m.IsAudioRoleDescription) // false first, then true
            .ToList();

        foreach (var aud in sortedAudio){
            string trackName = aud.Language.Name + (aud.IsAudioRoleDescription ? " [AD]" : "");
            args.Add("--audio-tracks 0");
            args.Add("--no-video");
            args.Add($"--track-name 0:\"{trackName}\"");
            args.Add($"--language 0:{aud.Language.Code}");

            if (Options.Defaults.Audio != null && Options.Defaults.Audio != Languages.DEFAULT_lang){
                if (Options.Defaults.Audio.Code == aud.Language.Code && !aud.IsAudioRoleDescription){
                    args.Add("--default-track 0");
                } else{
                    args.Add("--default-track 0:0");
                }
            } else{
                args.Add("--default-track 0:0");
            }

            if (aud.Delay != null && aud.Delay != 0){
                args.Add($"--sync 0:{aud.Delay}");
            }

            args.Add($"\"{Helpers.AddUncPrefixIfNeeded(aud.Path)}\"");
        }

        if (Options.Subtitles.Count > 0 && !Options.SkipSubMux){
            bool hasSignsSub = Options.Subtitles.Any(sub => sub.Signs && Options.Defaults.Sub?.Code == sub.Language.Code);

            var sortedSubtitles = Options.Subtitles
                .OrderBy(sub => Options.SubLangList.IndexOf(sub.Language.CrLocale) != -1
                    ? Options.SubLangList.IndexOf(sub.Language.CrLocale)
                    : int.MaxValue)
                .ThenBy(sub => sub.ClosedCaption ? 2 : sub.Signs ? 1 : 0)
                .ToList();

            foreach (var subObj in sortedSubtitles){
                bool isForced = false;
                if (subObj.Delay.HasValue){
                    double delay = subObj.Delay ?? 0;
                    args.Add($"--sync 0:{delay}");
                }

                string trackNameExtra = subObj.ClosedCaption ? $" {Options.CcTag}" : "";
                trackNameExtra += subObj.Signs ? " Signs" : "";

                string trackName = $"0:\"{(subObj.Language.Language ?? subObj.Language.Name) + trackNameExtra}\"";
                args.Add($"--track-name {trackName}");
                args.Add($"--language 0:\"{subObj.Language.Code}\"");
                if (Options.Defaults.Sub != null && Options.Defaults.Sub != Languages.DEFAULT_lang){
                    if (Options.Defaults.Sub.Code == subObj.Language.Code &&
                        (Options.DefaultSubSigns == subObj.Signs || Options.DefaultSubSigns && !hasSignsSub) && subObj.ClosedCaption == false){
                        args.Add("--default-track 0");
                        if (Options.DefaultSubForcedDisplay){
                            args.Add("--forced-track 0:yes");
                            isForced = true;
                        }
                    } else{
                        args.Add("--default-track 0:0");
                    }
                } else{
                    args.Add("--default-track 0:0");
                }

                if (subObj.ClosedCaption && Options.CcSubsMuxingFlag){
                    args.Add("--hearing-impaired-flag 0:yes");
                }

                if (subObj.Signs && Options.SignsSubsAsForced && !isForced){
                    args.Add("--forced-track 0:yes");
                }

                args.Add($"\"{Helpers.AddUncPrefixIfNeeded(subObj.File)}\"");
            }
        } else{
            args.Add("--no-subtitles");
        }

        if (Options.Fonts is{ Count: > 0 }){
            foreach (var font in Options.Fonts){
                args.Add($"--attachment-name \"{font.Name}\"");
                args.Add($"--attachment-mime-type \"{font.Mime}\"");
                args.Add($"--attach-file \"{Helpers.AddUncPrefixIfNeeded(font.Path)}\"");
            }
        } else{
            args.Add("--no-attachments");
        }

        if (Options.Chapters is{ Count: > 0 }){
            args.Add($"--chapters \"{Helpers.AddUncPrefixIfNeeded(Options.Chapters[0].Path)}\"");
        }

        if (!string.IsNullOrEmpty(Options.VideoTitle)){
            args.Add($"--title \"{Options.VideoTitle}\"");
        }

        if (Options.Description is{ Count: > 0 }){
            args.Add($"--global-tags \"{Helpers.AddUncPrefixIfNeeded(Options.Description[0].Path)}\"");
        }

        if (Options.Cover.Count > 0){
            if (File.Exists(Options.Cover.First().Path)){
                args.Add($"--attach-file \"{Options.Cover.First().Path}\"");
                args.Add($"--attachment-mime-type image/png");
                args.Add($"--attachment-name cover.png");
            }
        }


        return string.Join(" ", args);
    }

    public async Task<double> ProcessVideo(string baseVideoPath, string compareVideoPath){
        string baseFramesDir, baseFramesDirEnd;
        string compareFramesDir, compareFramesDirEnd;
        string cleanupDir;
        try{
            var tempDir = CfgManager.PathTEMP_DIR;
            string uuid = Guid.NewGuid().ToString();

            cleanupDir = Path.Combine(tempDir, uuid);
            baseFramesDir = Path.Combine(tempDir, uuid, "base_frames_start");
            baseFramesDirEnd = Path.Combine(tempDir, uuid, "base_frames_end");
            compareFramesDir = Path.Combine(tempDir, uuid, "compare_frames_start");
            compareFramesDirEnd = Path.Combine(tempDir, uuid, "compare_frames_end");

            Directory.CreateDirectory(baseFramesDir);
            Directory.CreateDirectory(baseFramesDirEnd);
            Directory.CreateDirectory(compareFramesDir);
            Directory.CreateDirectory(compareFramesDirEnd);
        } catch (Exception e){
            Console.Error.WriteLine(e);
            return -100;
        }

        try{
            var extractFramesBaseStart = await SyncingHelper.ExtractFrames(baseVideoPath, baseFramesDir, 0, 120);
            var extractFramesCompareStart = await SyncingHelper.ExtractFrames(compareVideoPath, compareFramesDir, 0, 120);

            TimeSpan? baseVideoDurationTimeSpan = await Helpers.GetMediaDurationAsync(CfgManager.PathFFMPEG, baseVideoPath);
            TimeSpan? compareVideoDurationTimeSpan = await Helpers.GetMediaDurationAsync(CfgManager.PathFFMPEG, compareVideoPath);

            if (baseVideoDurationTimeSpan == null || compareVideoDurationTimeSpan == null){
                Console.Error.WriteLine("Failed to retrieve video durations");
                return -100;
            }

            var extractFramesBaseEnd = await SyncingHelper.ExtractFrames(baseVideoPath, baseFramesDirEnd, baseVideoDurationTimeSpan.Value.TotalSeconds - 360, 360);
            var extractFramesCompareEnd = await SyncingHelper.ExtractFrames(compareVideoPath, compareFramesDirEnd, compareVideoDurationTimeSpan.Value.TotalSeconds - 360, 360);

            if (!extractFramesBaseStart.IsOk || !extractFramesCompareStart.IsOk || !extractFramesBaseEnd.IsOk || !extractFramesCompareEnd.IsOk){
                Console.Error.WriteLine("Failed to extract Frames to Compare");
                return -100;
            }


            // Load frames from start of the videos
            var baseFramesStart = Directory.GetFiles(baseFramesDir).Select(fp => new FrameData{
                FilePath = fp,
                Time = GetTimeFromFileName(fp, extractFramesBaseStart.frameRate)
            }).ToList();

            var compareFramesStart = Directory.GetFiles(compareFramesDir).Select(fp => new FrameData{
                FilePath = fp,
                Time = GetTimeFromFileName(fp, extractFramesCompareStart.frameRate)
            }).ToList();

            // Load frames from end of the videos
            var baseFramesEnd = Directory.GetFiles(baseFramesDirEnd).Select(fp => new FrameData{
                FilePath = fp,
                Time = GetTimeFromFileName(fp, extractFramesBaseEnd.frameRate)
            }).ToList();

            var compareFramesEnd = Directory.GetFiles(compareFramesDirEnd).Select(fp => new FrameData{
                FilePath = fp,
                Time = GetTimeFromFileName(fp, extractFramesCompareEnd.frameRate)
            }).ToList();


            // Calculate offsets
            var startOffset = SyncingHelper.CalculateOffset(baseFramesStart, compareFramesStart);
            var endOffset = SyncingHelper.CalculateOffset(baseFramesEnd, compareFramesEnd, true);

            var lengthDiff = (baseVideoDurationTimeSpan.Value.TotalMicroseconds - compareVideoDurationTimeSpan.Value.TotalMicroseconds) / 1000000;

            endOffset += lengthDiff;

            Console.WriteLine($"Start offset: {startOffset} seconds");
            Console.WriteLine($"End offset: {endOffset} seconds");

            CleanupDirectory(cleanupDir);

            baseFramesStart.Clear();
            baseFramesEnd.Clear();
            compareFramesStart.Clear();
            compareFramesEnd.Clear();

            var difference = Math.Abs(startOffset - endOffset);

            switch (difference){
                case < 0.1:
                    return startOffset;
                case > 1:
                    Console.Error.WriteLine($"Couldn't sync dub:");
                    Console.Error.WriteLine($"\tStart offset: {startOffset} seconds");
                    Console.Error.WriteLine($"\tEnd offset: {endOffset} seconds");
                    Console.Error.WriteLine($"\tVideo length difference: {lengthDiff} seconds");
                    return -100;
                default:
                    return endOffset;
            }
        } catch (Exception e){
            Console.Error.WriteLine(e);
            return -100;
        }
    }

    private static void CleanupDirectory(string dirPath){
        if (Directory.Exists(dirPath)){
            Directory.Delete(dirPath, true);
        }
    }

    private static double GetTimeFromFileName(string fileName, double frameRate){
        var match = Regex.Match(Path.GetFileName(fileName), @"frame(\d+)");
        if (match.Success){
            return int.Parse(match.Groups[1].Value) / frameRate;
        }

        return 0;
    }


    public async Task<bool> Merge(string type, string bin){
        string command = type switch{
            "ffmpeg" => FFmpeg(),
            "mkvmerge" => MkvMerge(),
            _ => ""
        };

        if (string.IsNullOrEmpty(command)){
            Console.Error.WriteLine("Unable to merge files.");
            return false;
        }

        Console.WriteLine($"[{type}] Started merging");
        var result = await Helpers.ExecuteCommandAsync(type, bin, command);

        if (!result.IsOk && type == "mkvmerge" && result.ErrorCode == 1){
            Console.Error.WriteLine($"[{type}] Mkvmerge finished with at least one warning");
        } else if (!result.IsOk){
            Console.Error.WriteLine($"[{type}] Merging failed with exit code {result.ErrorCode}");
            Console.Error.WriteLine($"[{type}] Merging failed command: {command}");
            return false;
        } else{
            Console.WriteLine($"[{type} Done]");
        }

        return true;
    }


    public void CleanUp(){
        // Combine all media file lists and iterate through them
        var allMediaFiles = Options.OnlyAudio.Concat(Options.OnlyVid)
            .ToList();
        allMediaFiles.ForEach(file => Helpers.DeleteFile(file.Path));
        allMediaFiles.ForEach(file => Helpers.DeleteFile(file.Path + ".resume"));
        allMediaFiles.ForEach(file => Helpers.DeleteFile(file.Path + ".new.resume"));

        Options.Description?.ForEach(description => Helpers.DeleteFile(description.Path));

        Options.Cover?.ForEach(cover => Helpers.DeleteFile(cover.Path));

        // Delete chapter files if any
        Options.Chapters?.ForEach(chapter => Helpers.DeleteFile(chapter.Path));

        if (!Options.SkipSubMux){
            // Delete subtitle files
            Options.Subtitles.ForEach(subtitle => Helpers.DeleteFile(subtitle.File));
        }
    }
}

public class MergerInput{
    public string Path{ get; set; }
    public LanguageItem Language{ get; set; }
    public int? Duration{ get; set; }
    public int? Delay{ get; set; }
    public bool IsAudioRoleDescription{ get; set; }
    public int? Bitrate{ get; set; }
}

public class SubtitleInput{
    public LanguageItem Language{ get; set; }
    public string File{ get; set; }
    public bool ClosedCaption{ get; set; }
    public bool Signs{ get; set; }
    public int? Delay{ get; set; }

    public DownloadedMedia? RelatedVideoDownloadMedia;
}

public class ParsedFont{
    public string Name{ get; set; }
    public string Path{ get; set; }
    public string Mime{ get; set; }
}

public class CrunchyMuxOptions{
    public List<string> DubLangList{ get; set; } = new List<string>();
    public List<string> SubLangList{ get; set; } = new List<string>();
    public string Output{ get; set; }
    public bool SkipSubMux{ get; set; }
    public bool? KeepAllVideos{ get; set; }
    public bool? Novids{ get; set; }
    public bool Mp4{ get; set; }
    public bool Mp3{ get; set; }
    public bool MuxFonts{ get; set; }
    public bool MuxCover{ get; set; }
    public bool MuxDescription{ get; set; }
    public string ForceMuxer{ get; set; }
    public bool? NoCleanup{ get; set; }
    public string VideoTitle{ get; set; }
    public List<string> FfmpegOptions{ get; set; } = new List<string>();
    public List<string> MkvmergeOptions{ get; set; } = new List<string>();
    public LanguageItem? DefaultSub{ get; set; }
    public LanguageItem? DefaultAudio{ get; set; }
    public string CcTag{ get; set; }
    public bool SyncTiming{ get; set; }
    public bool DlVideoOnce{ get; set; }

    public bool DefaultSubSigns{ get; set; }

    public bool DefaultSubForcedDisplay{ get; set; }
    public bool CcSubsMuxingFlag{ get; set; }
    public bool SignsSubsAsForced{ get; set; }
}

public class MergerOptions{
    public List<string> DubLangList{ get; set; } = new List<string>();
    public List<string> SubLangList{ get; set; } = new List<string>();
    public List<MergerInput> OnlyVid{ get; set; } = new List<MergerInput>();
    public List<MergerInput> OnlyAudio{ get; set; } = new List<MergerInput>();
    public List<SubtitleInput> Subtitles{ get; set; } = new List<SubtitleInput>();
    public List<MergerInput>? Chapters{ get; set; } = new List<MergerInput>();
    public string CcTag{ get; set; }
    public string Output{ get; set; }
    public string VideoTitle{ get; set; }
    public bool? KeepAllVideos{ get; set; }
    public List<ParsedFont> Fonts{ get; set; } = new List<ParsedFont>();
    public bool SkipSubMux{ get; set; }
    public MuxOptions Options{ get; set; }
    public Defaults Defaults{ get; set; }
    public bool mp3{ get; set; }
    public bool DefaultSubSigns{ get; set; }
    public bool DefaultSubForcedDisplay{ get; set; }
    public bool CcSubsMuxingFlag{ get; set; }
    public bool SignsSubsAsForced{ get; set; }
    public List<MergerInput> Description{ get; set; } = new List<MergerInput>();
    public List<MergerInput> Cover{ get; set; } = [];
}

public class MuxOptions{
    public List<string>? ffmpeg{ get; set; }
    public List<string>? mkvmerge{ get; set; }
}

public class Defaults{
    public LanguageItem? Audio{ get; set; }
    public LanguageItem? Sub{ get; set; }
}