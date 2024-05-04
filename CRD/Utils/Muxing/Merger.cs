using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CRD.Utils.Structs;

namespace CRD.Utils.Muxing;

public class Merger{
    private MergerOptions options;

    public Merger(MergerOptions options){
        this.options = options;
        if (this.options.SkipSubMux != null && this.options.SkipSubMux == true){
            this.options.Subtitles = new List<SubtitleInput>();
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
            foreach (var vid in options.VideoAndAudio){
            if (vid.Delay != null && hasVideo){
                args.Add($"-itsoffset -{Math.Ceiling((double)vid.Delay * 1000)}ms");
            }

            args.Add($"-i \"{vid.Path}\"");
            if (!hasVideo || options.KeepAllVideos == true){
                metaData.Add($"-map {index}:a -map {index}:v");
                metaData.Add($"-metadata:s:a:{audioIndex} language={vid.Language.Code}");
                metaData.Add($"-metadata:s:v:{index} title=\"{options.VideoTitle}\"");
                hasVideo = true;
            } else{
                metaData.Add($"-map {index}:a");
                metaData.Add($"-metadata:s:a:{audioIndex} language={vid.Language.Code}");
            }

            audioIndex++;
            index++;
        }

        foreach (var vid in options.OnlyVid){
            if (!hasVideo || options.KeepAllVideos == true){
                args.Add($"-i \"{vid.Path}\"");
                metaData.Add($"-map {index} -map -{index}:a");
                metaData.Add($"-metadata:s:v:{index} title=\"{options.VideoTitle}\"");
                hasVideo = true;
                index++;
            }
        }

        foreach (var aud in options.OnlyAudio){
            args.Add($"-i \"{aud.Path}\"");
            metaData.Add($"-map {index}");
            metaData.Add($"-metadata:s:a:{audioIndex} language={aud.Language.Code}");
            index++;
            audioIndex++;
        }

        foreach (var sub in options.Subtitles.Select((value, i) => new{ value, i })){
            if (sub.value.Delay != null){
                args.Add($"-itsoffset -{Math.Ceiling((double)sub.value.Delay * 1000)}ms");
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
        if (options.Options.ffmpeg?.Count > 0){
            args.AddRange(options.Options.ffmpeg);
        }
        args.Add($"\"{options.Output}\"");

        return string.Join(" ", args);
        } 

        
        args.Add($"-i \"{options.OnlyAudio[0].Path}\"");
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

                string trackName = $"{(options.VideoTitle ?? vid.Language.Name)}{(options.Simul == true ? " [Simulcast]" : " [Uncut]")}";
                args.Add($"--track-name 0:\"{trackName}\"");
                args.Add($"--language 0:{vid.Language.Code}");

                hasVideo = true;
                args.Add($"\"{vid.Path}\"");
            }
        }

        foreach (var vid in options.VideoAndAudio){
            string audioTrackNum = options.InverseTrackOrder == true ? "0" : "1";
            string videoTrackNum = options.InverseTrackOrder == true ? "1" : "0";

            if (vid.Delay.HasValue){
                double delay = vid.Delay ?? 0;
                args.Add($"--sync {audioTrackNum}:-{Math.Ceiling(delay * 1000)}");
            }

            if (!hasVideo || options.KeepAllVideos == true){
                args.Add($"--video-tracks {videoTrackNum}");
                args.Add($"--audio-tracks {audioTrackNum}");

                string trackName = $"{(options.VideoTitle ?? vid.Language.Name)}{(options.Simul == true ? " [Simulcast]" : " [Uncut]")}";
                args.Add($"--track-name 0:\"{trackName}\""); // Assuming trackName applies to video if present
                args.Add($"--language {audioTrackNum}:{vid.Language.Code}");

                if (options.Defaults.Audio.Code == vid.Language.Code){
                    args.Add($"--default-track {audioTrackNum}");
                } else{
                    args.Add($"--default-track {audioTrackNum}:0");
                }

                hasVideo = true;
            } else{
                args.Add("--no-video");
                args.Add($"--audio-tracks {audioTrackNum}");

                if (options.Defaults.Audio.Code == vid.Language.Code){
                    args.Add($"--default-track {audioTrackNum}");
                } else{
                    args.Add($"--default-track {audioTrackNum}:0");
                }

                args.Add($"--track-name {audioTrackNum}:\"{vid.Language.Name}\"");
                args.Add($"--language {audioTrackNum}:{vid.Language.Code}");
            }

            args.Add($"\"{vid.Path}\"");
        }

        foreach (var aud in options.OnlyAudio){
            string trackName = aud.Language.Name;
            args.Add($"--track-name 0:\"{trackName}\"");
            args.Add($"--language 0:{aud.Language.Code}");
            args.Add("--no-video");
            args.Add("--audio-tracks 0");

            if (options.Defaults.Audio.Code == aud.Language.Code){
                args.Add("--default-track 0");
            } else{
                args.Add("--default-track 0:0");
            }

            args.Add($"\"{aud.Path}\"");
        }

        if (options.Subtitles.Count > 0){
            foreach (var subObj in options.Subtitles){
                if (subObj.Delay.HasValue){
                    double delay = subObj.Delay ?? 0;
                    args.Add($"--sync 0:-{Math.Ceiling(delay * 1000)}");
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

        if (options.Fonts != null && options.Fonts.Count > 0){
            foreach (var font in options.Fonts){
                args.Add($"--attachment-name \"{font.Name}\"");
                args.Add($"--attachment-mime-type \"{font.Mime}\"");
                args.Add($"--attach-file \"{font.Path}\"");
            }
        } else{
            args.Add("--no-attachments");
        }

        if (options.Chapters != null && options.Chapters.Count > 0){
            args.Add($"--chapters \"{options.Chapters[0].Path}\"");
        }


        return string.Join(" ", args);
    }

    // public async Task CreateDelays(){
    //     // Don't bother scanning if there is only 1 vna stream
    //     if (options.VideoAndAudio.Count > 1){
    //         var bin = await YamlCfg.LoadBinCfg();
    //         var vnas = this.options.VideoAndAudio;
    //
    //         // Get and set durations on each videoAndAudio Stream
    //         foreach (var vna in vnas){
    //             var streamInfo = await FFProbe(vna.Path, bin.FFProbe);
    //             var videoInfo = streamInfo.Streams.Where(stream => stream.CodecType == "video").FirstOrDefault();
    //             vna.Duration = int.Parse(videoInfo.Duration);
    //         }
    //
    //         // Sort videoAndAudio streams by duration (shortest first)
    //         vnas.Sort((a, b) => {
    //             if (a.Duration == 0 || b.Duration == 0) return -1;
    //             return a.Duration.CompareTo(b.Duration);
    //         });
    //
    //         // Set Delays
    //         var shortestDuration = vnas[0].Duration;
    //         foreach (var (vna, index) in vnas.Select((vna, index) => (vna, index))){
    //             // Don't calculate the shortestDuration track
    //             if (index == 0){
    //                 if (!vna.IsPrimary)
    //                     Console.WriteLine("Shortest video isn't primary, this might lead to problems with subtitles. Please report on github or discord if you experience issues.");
    //                 continue;
    //             }
    //
    //             if (vna.Duration > 0 && shortestDuration > 0){
    //                 // Calculate the tracks delay
    //                 vna.Delay = Math.Ceiling((vna.Duration - shortestDuration) * 1000) / 1000;
    //
    //                 var subtitles = this.options.Subtitles.Where(sub => sub.Language.Code == vna.Lang.Code).ToList();
    //                 foreach (var (sub, subIndex) in subtitles.Select((sub, subIndex) => (sub, subIndex))){
    //                     if (vna.IsPrimary)
    //                         subtitles[subIndex].Delay = vna.Delay;
    //                     else if (sub.ClosedCaption)
    //                         subtitles[subIndex].Delay = vna.Delay;
    //                 }
    //             }
    //         }
    //     }
    // }


    public async Task Merge(string type, string bin){
        string command = type switch{
            "ffmpeg" => FFmpeg(),
            "mkvmerge" => MkvMerge(),
            _ => ""
        };

        if (string.IsNullOrEmpty(command)){
            Console.WriteLine("Unable to merge files.");
            return;
        }

        Console.WriteLine($"[{type}] Started merging");
        var result = await Helpers.ExecuteCommandAsync(type, bin, command);

        if (!result.IsOk && type == "mkvmerge" && result.ErrorCode == 1){
            Console.WriteLine($"[{type}] Mkvmerge finished with at least one warning");
        } else if (!result.IsOk){
            Console.WriteLine($"[{type}] Merging failed with exit code {result.ErrorCode}");
        } else{
            Console.WriteLine($"[{type} Done]");
        }
    }


    public void CleanUp(){
        // Combine all media file lists and iterate through them
        var allMediaFiles = options.OnlyAudio.Concat(options.OnlyVid)
            .Concat(options.VideoAndAudio).ToList();
        allMediaFiles.ForEach(file => DeleteFile(file.Path));
        allMediaFiles.ForEach(file => DeleteFile(file.Path + ".resume"));

        // Delete chapter files if any
        options.Chapters?.ForEach(chapter => DeleteFile(chapter.Path));

        // Delete subtitle files
        options.Subtitles.ForEach(subtitle => DeleteFile(subtitle.File));
    }

    private void DeleteFile(string filePath){
        try{
            if (File.Exists(filePath)){
                File.Delete(filePath);
            }
        } catch (Exception ex){
            Console.WriteLine($"Failed to delete file {filePath}. Error: {ex.Message}");
            // Handle exceptions if you need to log them or throw
        }
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
    public List<MergerInput> VideoAndAudio{ get; set; } = new List<MergerInput>();
    public List<MergerInput> OnlyVid{ get; set; } = new List<MergerInput>();
    public List<MergerInput> OnlyAudio{ get; set; } = new List<MergerInput>();
    public List<SubtitleInput> Subtitles{ get; set; } = new List<SubtitleInput>();
    public List<MergerInput> Chapters{ get; set; } = new List<MergerInput>();
    public string CcTag{ get; set; }
    public string Output{ get; set; }
    public string VideoTitle{ get; set; }
    public bool? Simul{ get; set; }
    public bool? InverseTrackOrder{ get; set; }
    public bool? KeepAllVideos{ get; set; }
    public List<ParsedFont> Fonts{ get; set; } = new List<ParsedFont>();
    public bool? SkipSubMux{ get; set; }
    public MuxOptions Options{ get; set; }
    public Defaults Defaults{ get; set; }
    
    public bool mp3{ get; set; }
}

public class MuxOptions{
    public List<string>? ffmpeg{ get; set; }
    public List<string>? mkvmerge{ get; set; }
}

public class Defaults{
    public LanguageItem Audio{ get; set; }
    public LanguageItem Sub{ get; set; }
}