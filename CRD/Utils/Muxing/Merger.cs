using System;
using System.Linq;
using System.Threading.Tasks;
using CRD.Utils.Muxing.Commands;
using CRD.Utils.Muxing.Structs;

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
        return new FFmpegCommandBuilder(Options).Build();
    }

    public string MkvMerge(){
        return new MkvMergeCommandBuilder(Options).Build();
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
        var result = await Helpers.ExecuteCommandAsync(bin, command);

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
        var allMediaFiles = Options.OnlyAudio.Concat(Options.OnlyVid).Concat(Options.VideoAndAudio)
            .ToList();
        allMediaFiles.ForEach(file => Helpers.DeleteFile(file.Path));
        allMediaFiles.ForEach(file => Helpers.DeleteFile(file.Path + ".resume"));
        allMediaFiles.ForEach(file => Helpers.DeleteFile(file.Path + ".new.resume"));

        Options.Description?.ForEach(description => Helpers.DeleteFile(description.Path));
        Options.Cover.ForEach(cover => Helpers.DeleteFile(cover.Path));

        // Delete chapter files if any
        Options.Chapters?.ForEach(chapter => Helpers.DeleteFile(chapter.Path));

        if (!Options.SkipSubMux){
            // Delete subtitle files
            Options.Subtitles.ForEach(subtitle => Helpers.DeleteFile(subtitle.File));
        }
    }
}
