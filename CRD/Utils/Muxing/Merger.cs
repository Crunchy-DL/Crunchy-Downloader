using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using CRD.Utils.Structs;
using DynamicData;

namespace CRD.Utils.Muxing;

public class Merger{
    public MergerOptions options;

    public Merger(MergerOptions options){
        this.options = options;
        if (this.options.SkipSubMux != null && this.options.SkipSubMux == true){
            this.options.Subtitles = new();
        }

        if (this.options.VideoTitle != null && this.options.VideoTitle.Length > 0){
            this.options.VideoTitle = this.options.VideoTitle.Replace("\"", "'");
        }
    }

    public string FFmpeg(){
        List<string> args = new List<string>();

        List<string> metaData = new List<string>();

        var index = 0;
        var audioIndex = 0;
        var hasVideo = false;

        if (!options.mp3){
            foreach (var vid in options.OnlyVid){
                if (!hasVideo || options.KeepAllVideos == true){
                    args.Add($"-i \"{vid.Path}\"");
                    metaData.Add($"-map {index}:v");
                    metaData.Add($"-metadata:s:v:{index} title=\"{(vid.Language.Name)}\"");
                    hasVideo = true;
                    index++;
                }
            }

            foreach (var aud in options.OnlyAudio){
                if (aud.Delay != null && aud.Delay != 0){
                    double delay = aud.Delay / 1000.0 ?? 0;
                    args.Add($"-itsoffset {delay.ToString(CultureInfo.InvariantCulture)}");
                }

                args.Add($"-i \"{aud.Path}\"");
                metaData.Add($"-map {index}:a");
                metaData.Add($"-metadata:s:a:{audioIndex} language={aud.Language.Code}");
                index++;
                audioIndex++;
            }

            if (options.Chapters != null && options.Chapters.Count > 0){
                Helpers.ConvertChapterFileForFFMPEG(options.Chapters[0].Path);

                args.Add($"-i \"{options.Chapters[0].Path}\"");
                metaData.Add($"-map_metadata {index}");
                index++;
            }

            foreach (var sub in options.Subtitles.Select((value, i) => new{ value, i })){
                if (sub.value.Delay != null && sub.value.Delay != 0){
                    double delay = sub.value.Delay / 1000.0 ?? 0;
                    args.Add($"-itsoffset {delay.ToString(CultureInfo.InvariantCulture)}");
                }

                args.Add($"-i \"{sub.value.File}\"");
            }

            if (options.Output.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase)){
                if (options.Fonts != null){
                    int fontIndex = 0;
                    foreach (var font in options.Fonts){
                        args.Add($"-attach {font.Path} -metadata:s:t:{fontIndex} mimetype={font.Mime}");
                        fontIndex++;
                    }
                }
            }

            args.AddRange(metaData);
            args.AddRange(options.Subtitles.Select((sub, subIndex) => $"-map {subIndex + index}"));
            args.Add("-c:v copy");
            args.Add("-c:a copy");
            args.Add(options.Output.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ? "-c:s mov_text" : "-c:s ass");
            args.AddRange(options.Subtitles.Select((sub, subindex) =>
                $"-metadata:s:s:{subindex} title=\"{sub.Language.Language ?? sub.Language.Name}{(sub.ClosedCaption == true ? $" {options.CcTag}" : "")}{(sub.Signs == true ? " Signs" : "")}\" -metadata:s:s:{subindex} language={sub.Language.Code}"));


            if (!string.IsNullOrEmpty(options.VideoTitle)){
                args.Add($"-metadata title=\"{options.VideoTitle}\"");
            }

            if (options.Description is{ Count: > 0 }){
                XmlDocument doc = new XmlDocument();
                doc.Load(options.Description[0].Path);
                XmlNode? node = doc.SelectSingleNode("//Tag/Simple[Name='DESCRIPTION']/String");
                args.Add($"-metadata comment=\"{node?.InnerText ?? string.Empty}\"");
            }

            if (options.Options.ffmpeg?.Count > 0){
                args.AddRange(options.Options.ffmpeg);
            }

            args.Add($"\"{options.Output}\"");

            return string.Join(" ", args);
        }


        foreach (var aud in options.OnlyAudio){
            args.Add($"-i \"{aud.Path}\"");
            metaData.Add($"-map {index}");
            metaData.Add($"-metadata:s:a:{audioIndex} language={aud.Language.Code}");
            index++;
            audioIndex++;
        }

        args.Add("-acodec libmp3lame");
        args.Add("-ab 192k");
        args.Add($"\"{options.Output}\"");
        return string.Join(" ", args);
    }


    public string MkvMerge(){
        List<string> args = new List<string>();

        bool hasVideo = false;

        args.Add($"-o \"{options.Output}\"");
        if (options.Options.mkvmerge != null){
            args.AddRange(options.Options.mkvmerge);
        }


        foreach (var vid in options.OnlyVid){
            if (!hasVideo || options.KeepAllVideos == true){
                args.Add("--video-tracks 0");
                args.Add("--no-audio");

                string trackName = $"{(vid.Language.Name)}";
                args.Add($"--track-name 0:\"{trackName}\"");
                args.Add($"--language 0:{vid.Language.Code}");

                hasVideo = true;
                args.Add($"\"{vid.Path}\"");
            }
        }

        foreach (var aud in options.OnlyAudio){
            string trackName = aud.Language.Name;
            args.Add("--audio-tracks 0");
            args.Add("--no-video");
            args.Add($"--track-name 0:\"{trackName}\"");
            args.Add($"--language 0:{aud.Language.Code}");


            if (options.Defaults.Audio.Code == aud.Language.Code){
                args.Add("--default-track 0");
            } else{
                args.Add("--default-track 0:0");
            }

            if (aud.Delay != null && aud.Delay != 0){
                args.Add($"--sync 0:{aud.Delay}");
            }

            args.Add($"\"{aud.Path}\"");
        }

        if (options.Subtitles.Count > 0){
            foreach (var subObj in options.Subtitles){
                if (subObj.Delay.HasValue){
                    double delay = subObj.Delay ?? 0;
                    args.Add($"--sync 0:{delay}");
                }

                string trackNameExtra = subObj.ClosedCaption == true ? $" {options.CcTag}" : "";
                trackNameExtra += subObj.Signs == true ? " Signs" : "";

                string trackName = $"0:\"{(subObj.Language.Language ?? subObj.Language.Name) + trackNameExtra}\"";
                args.Add($"--track-name {trackName}");
                args.Add($"--language 0:\"{subObj.Language.Code}\"");

                if (options.Defaults.Sub.Code == subObj.Language.Code && subObj.ClosedCaption == false){
                    args.Add("--default-track 0");
                } else{
                    args.Add("--default-track 0:0");
                }

                args.Add($"\"{subObj.File}\"");
            }
        } else{
            args.Add("--no-subtitles");
        }

        if (options.Fonts is{ Count: > 0 }){
            foreach (var font in options.Fonts){
                args.Add($"--attachment-name \"{font.Name}\"");
                args.Add($"--attachment-mime-type \"{font.Mime}\"");
                args.Add($"--attach-file \"{font.Path}\"");
            }
        } else{
            args.Add("--no-attachments");
        }

        if (options.Chapters is{ Count: > 0 }){
            args.Add($"--chapters \"{options.Chapters[0].Path}\"");
        }

        if (!string.IsNullOrEmpty(options.VideoTitle)){
            args.Add($"--title \"{options.VideoTitle}\"");
        }

        if (options.Description is{ Count: > 0 }){
            args.Add($"--global-tags \"{options.Description[0].Path}\"");
        }


        return string.Join(" ", args);
    }


    public async Task<double> ProcessVideo(string baseVideoPath, string compareVideoPath){
        var tempDir = Path.GetTempPath(); //TODO - maybe move this out of temp
        var baseFramesDir = Path.Combine(tempDir, "base_frames");
        var compareFramesDir = Path.Combine(tempDir, "compare_frames");

        Directory.CreateDirectory(baseFramesDir);
        Directory.CreateDirectory(compareFramesDir);

        var extractFramesBase = await SyncingHelper.ExtractFrames(baseVideoPath, baseFramesDir, 0, 60);
        var extractFramesCompare = await SyncingHelper.ExtractFrames(compareVideoPath, compareFramesDir, 0, 60);

        if (!extractFramesBase.IsOk || !extractFramesCompare.IsOk){
            Console.Error.WriteLine("Failed to extract Frames to Compare");
            return 0;
        }

        var baseFrames = Directory.GetFiles(baseFramesDir).Select(fp => new FrameData{
            FilePath = fp,
            Time = GetTimeFromFileName(fp, extractFramesBase.frameRate)
        }).ToList();

        var compareFrames = Directory.GetFiles(compareFramesDir).Select(fp => new FrameData{
            FilePath = fp,
            Time = GetTimeFromFileName(fp, extractFramesBase.frameRate)
        }).ToList();

        var offset = SyncingHelper.CalculateOffset(baseFrames, compareFrames);
        Console.WriteLine($"Calculated offset: {offset} seconds");

        CleanupDirectory(baseFramesDir);
        CleanupDirectory(compareFramesDir);

        return offset;
    }

    private static void CleanupDirectory(string dirPath){
        if (Directory.Exists(dirPath)){
            Directory.Delete(dirPath, true);
        }
    }

    private static double GetTimeFromFileName(string fileName, double frameRate){
        var match = Regex.Match(Path.GetFileName(fileName), @"frame(\d+)");
        if (match.Success){
            return int.Parse(match.Groups[1].Value) / frameRate; // Assuming 30 fps
        }

        return 0;
    }


    public async Task Merge(string type, string bin){
        string command = type switch{
            "ffmpeg" => FFmpeg(),
            "mkvmerge" => MkvMerge(),
            _ => ""
        };

        if (string.IsNullOrEmpty(command)){
            Console.Error.WriteLine("Unable to merge files.");
            return;
        }

        Console.WriteLine($"[{type}] Started merging");
        var result = await Helpers.ExecuteCommandAsync(type, bin, command);

        if (!result.IsOk && type == "mkvmerge" && result.ErrorCode == 1){
            Console.WriteLine($"[{type}] Mkvmerge finished with at least one warning");
        } else if (!result.IsOk){
            Console.Error.WriteLine($"[{type}] Merging failed with exit code {result.ErrorCode}");
        } else{
            Console.WriteLine($"[{type} Done]");
        }
    }


    public void CleanUp(){
        // Combine all media file lists and iterate through them
        var allMediaFiles = options.OnlyAudio.Concat(options.OnlyVid)
            .ToList();
        allMediaFiles.ForEach(file => Helpers.DeleteFile(file.Path));
        allMediaFiles.ForEach(file => Helpers.DeleteFile(file.Path + ".resume"));

        options.Description?.ForEach(chapter => Helpers.DeleteFile(chapter.Path));

        // Delete chapter files if any
        options.Chapters?.ForEach(chapter => Helpers.DeleteFile(chapter.Path));

        // Delete subtitle files
        options.Subtitles.ForEach(subtitle => Helpers.DeleteFile(subtitle.File));
    }
}

public class MergerInput{
    public string Path{ get; set; }
    public LanguageItem Language{ get; set; }
    public int? Duration{ get; set; }
    public int? Delay{ get; set; }
    public bool? IsPrimary{ get; set; }
}

public class SubtitleInput{
    public LanguageItem Language{ get; set; }
    public string File{ get; set; }
    public bool? ClosedCaption{ get; set; }
    public bool? Signs{ get; set; }
    public int? Delay{ get; set; }

    public DownloadedMedia? RelatedVideoDownloadMedia;
}

public class ParsedFont{
    public string Name{ get; set; }
    public string Path{ get; set; }
    public string Mime{ get; set; }
}

public class CrunchyMuxOptions{
    public string Output{ get; set; }
    public bool? SkipSubMux{ get; set; }
    public bool? KeepAllVideos{ get; set; }
    public bool? Novids{ get; set; }
    public bool Mp4{ get; set; }
    public bool MuxDescription{ get; set; }
    public string ForceMuxer{ get; set; }
    public bool? NoCleanup{ get; set; }
    public string VideoTitle{ get; set; }
    public List<string> FfmpegOptions{ get; set; } = new List<string>();
    public List<string> MkvmergeOptions{ get; set; } = new List<string>();
    public LanguageItem DefaultSub{ get; set; }
    public LanguageItem DefaultAudio{ get; set; }
    public string CcTag{ get; set; }
    public bool SyncTiming{ get; set; }
}

public class MergerOptions{
    public List<MergerInput> OnlyVid{ get; set; } = new List<MergerInput>();
    public List<MergerInput> OnlyAudio{ get; set; } = new List<MergerInput>();
    public List<SubtitleInput> Subtitles{ get; set; } = new List<SubtitleInput>();
    public List<MergerInput> Chapters{ get; set; } = new List<MergerInput>();
    public string CcTag{ get; set; }
    public string Output{ get; set; }
    public string VideoTitle{ get; set; }
    public bool? KeepAllVideos{ get; set; }
    public List<ParsedFont> Fonts{ get; set; } = new List<ParsedFont>();
    public bool? SkipSubMux{ get; set; }
    public MuxOptions Options{ get; set; }
    public Defaults Defaults{ get; set; }
    public bool mp3{ get; set; }
    public List<MergerInput> Description{ get; set; } = new List<MergerInput>();
}

public class MuxOptions{
    public List<string>? ffmpeg{ get; set; }
    public List<string>? mkvmerge{ get; set; }
}

public class Defaults{
    public LanguageItem Audio{ get; set; }
    public LanguageItem Sub{ get; set; }
}