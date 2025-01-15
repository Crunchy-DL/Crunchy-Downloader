using System;
using System.Collections.Generic;
using System.Linq;

namespace CRD.Utils.Ffmpeg_Encoding;

public class FfmpegEncoding{
    public static readonly List<VideoPreset> presets = new List<VideoPreset>{
        // AV1 Software
        new(){ PresetName = "AV1 1080p24", Codec = "libaom-av1", Resolution = "1920:1080", FrameRate = "24000/1001", Crf = 30, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "AV1 720p24", Codec = "libaom-av1", Resolution = "1280:720", FrameRate = "24000/1001", Crf = 30, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "AV1 480p24", Codec = "libaom-av1", Resolution = "854:480", FrameRate = "24000/1001", Crf = 30, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "AV1 360p24", Codec = "libaom-av1", Resolution = "640:360", FrameRate = "24000/1001", Crf = 30, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "AV1 240p24", Codec = "libaom-av1", Resolution = "426:240", FrameRate = "24000/1001", Crf = 30, AdditionalParameters ={ "-map 0" } },

        // H.265 Software
        new(){ PresetName = "H.265 1080p24", Codec = "libx265", Resolution = "1920:1080", FrameRate = "24000/1001", Crf = 28, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "H.265 720p24", Codec = "libx265", Resolution = "1280:720", FrameRate = "24000/1001", Crf = 28, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "H.265 480p24", Codec = "libx265", Resolution = "854:480", FrameRate = "24000/1001", Crf = 28, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "H.265 360p24", Codec = "libx265", Resolution = "640:360", FrameRate = "24000/1001", Crf = 28, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "H.265 240p24", Codec = "libx265", Resolution = "426:240", FrameRate = "24000/1001", Crf = 28, AdditionalParameters ={ "-map 0" } },

        // H.264 Software
        new(){ PresetName = "H.264 1080p24", Codec = "libx264", Resolution = "1920:1080", FrameRate = "24000/1001", Crf = 23, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "H.264 720p24", Codec = "libx264", Resolution = "1280:720", FrameRate = "24000/1001", Crf = 23, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "H.264 480p24", Codec = "libx264", Resolution = "854:480", FrameRate = "24000/1001", Crf = 23, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "H.264 360p24", Codec = "libx264", Resolution = "640:360", FrameRate = "24000/1001", Crf = 23, AdditionalParameters ={ "-map 0" } },
        new(){ PresetName = "H.264 240p24", Codec = "libx264", Resolution = "426:240", FrameRate = "24000/1001", Crf = 23, AdditionalParameters ={ "-map 0" } },
    };

    public static VideoPreset? GetPreset(string presetName){
        var preset = presets.FirstOrDefault(x => x.PresetName == presetName);
        if (preset != null){
            return preset;
        }

        Console.Error.WriteLine($"Preset {presetName} not found.");
        return null;
    }

    public static void AddPreset(VideoPreset preset){
        if (presets.Exists(x => x.PresetName == preset.PresetName)){
            Console.Error.WriteLine($"Preset {preset.PresetName} already exists.");
            return;
        }

        presets.Add(preset);
    }
}

public class VideoPreset{
    public string? PresetName{ get; set; }
    public string? Codec{ get; set; }
    public string? Resolution{ get; set; }
    public string? FrameRate{ get; set; }
    public int Crf{ get; set; }

    public List<string> AdditionalParameters{ get; set; } = new List<string>();
}