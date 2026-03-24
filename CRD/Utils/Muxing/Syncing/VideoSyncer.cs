using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CRD.Utils.Files;
using CRD.Utils.Structs;

namespace CRD.Utils.Muxing.Syncing;

public class VideoSyncer{
    public static VideoSyncer Instance{ get; } = new();

    public static async Task<(double offSet, double startOffset, double endOffset, double lengthDiff)> ProcessVideo(string baseVideoPath, string compareVideoPath){
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
            return (-100, 0, 0, 0);
        }

        try{
            var extractFramesBaseStart = await SyncingHelper.ExtractFrames(baseVideoPath, baseFramesDir, 0, 120);
            var extractFramesCompareStart = await SyncingHelper.ExtractFrames(compareVideoPath, compareFramesDir, 0, 120);

            TimeSpan? baseVideoDurationTimeSpan = await Helpers.GetMediaDurationAsync(CfgManager.PathFFMPEG, baseVideoPath);
            TimeSpan? compareVideoDurationTimeSpan = await Helpers.GetMediaDurationAsync(CfgManager.PathFFMPEG, compareVideoPath);

            if (baseVideoDurationTimeSpan == null || compareVideoDurationTimeSpan == null){
                Console.Error.WriteLine("Failed to retrieve video durations");
                return (-100, 0, 0, 0);
            }

            var extractFramesBaseEnd = await SyncingHelper.ExtractFrames(baseVideoPath, baseFramesDirEnd, baseVideoDurationTimeSpan.Value.TotalSeconds - 360, 360);
            var extractFramesCompareEnd = await SyncingHelper.ExtractFrames(compareVideoPath, compareFramesDirEnd, compareVideoDurationTimeSpan.Value.TotalSeconds - 360, 360);

            if (!extractFramesBaseStart.IsOk || !extractFramesCompareStart.IsOk || !extractFramesBaseEnd.IsOk || !extractFramesCompareEnd.IsOk){
                Console.Error.WriteLine("Failed to extract Frames to Compare");
                return (-100, 0, 0, 0);
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
                    return (startOffset, startOffset, endOffset, lengthDiff);
                case > 1:
                    Console.Error.WriteLine($"Couldn't sync dub:");
                    Console.Error.WriteLine($"\tStart offset: {startOffset} seconds");
                    Console.Error.WriteLine($"\tEnd offset: {endOffset} seconds");
                    Console.Error.WriteLine($"\tVideo length difference: {lengthDiff} seconds");
                    return (-100, startOffset, endOffset, lengthDiff);
                default:
                    return (endOffset, startOffset, endOffset, lengthDiff);
            }
        } catch (Exception e){
            Console.Error.WriteLine(e);
            return (-100, 0, 0, 0);
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
}