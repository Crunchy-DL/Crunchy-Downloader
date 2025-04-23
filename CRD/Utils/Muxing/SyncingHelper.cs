using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CRD.Downloader.Crunchyroll;
using CRD.Utils.Files;
using CRD.Utils.Structs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace CRD.Utils.Muxing;

public class SyncingHelper{
    public static async Task<(bool IsOk, int ErrorCode, double frameRate)> ExtractFrames(string videoPath, string outputDir, double offset, double duration){
        var ffmpegPath = CfgManager.PathFFMPEG;
        var arguments =
            $"{CrunchyrollManager.Instance.CrunOptions.FfmpegHwAccelFlag}-ss {offset} -t {duration} -i \"{videoPath}\" -vf \"select='gt(scene,0.1)',showinfo\" -vsync vfr -frame_pts true \"{outputDir}\\frame%05d.jpg\"";

        var output = "";

        try{
            using (var process = new Process()){
                process.StartInfo.FileName = ffmpegPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        Console.WriteLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)){
                        // Console.WriteLine($"{e.Data}");
                        output += e.Data;
                    }
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();
                bool isSuccess = process.ExitCode == 0;
                double frameRate = ExtractFrameRate(output);
                return (IsOk: isSuccess, ErrorCode: process.ExitCode, frameRate);
            }
        } catch (Exception ex){
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
            return (IsOk: false, ErrorCode: -1, 0);
        }
    }

    public static double ExtractFrameRate(string ffmpegOutput){
        var match = Regex.Match(ffmpegOutput, @"Stream #0:0.*?(\d+(?:\.\d+)?) fps");
        if (match.Success){
            return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        Console.Error.WriteLine("Failed to extract frame rate from FFmpeg output.");
        return 0;
    }

    private static double CalculateSSIM(float[] pixels1, float[] pixels2){
        double mean1 = pixels1.Average();
        double mean2 = pixels2.Average();

        double var1 = 0, var2 = 0, covariance = 0;
        int count = pixels1.Length;

        for (int i = 0; i < count; i++){
            var1 += (pixels1[i] - mean1) * (pixels1[i] - mean1);
            var2 += (pixels2[i] - mean2) * (pixels2[i] - mean2);
            covariance += (pixels1[i] - mean1) * (pixels2[i] - mean2);
        }

        var1 /= count - 1;
        var2 /= count - 1;
        covariance /= count - 1;

        double c1 = 0.01 * 0.01;
        double c2 = 0.03 * 0.03;

        double ssim = ((2 * mean1 * mean2 + c1) * (2 * covariance + c2)) /
                      ((mean1 * mean1 + mean2 * mean2 + c1) * (var1 + var2 + c2));

        return ssim;
    }

    private static float[] ExtractPixels(Image<Rgba32> image, int width, int height){
        float[] pixels = new float[width * height];
        int index = 0;

        image.ProcessPixelRows(accessor => {
            for (int y = 0; y < accessor.Height; y++){
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++){
                    pixels[index++] = row[x].R / 255f;
                    ;
                }
            }
        });

        return pixels;
    }

    public static (double ssim, double pixelDiff) ComputeSSIM(string imagePath1, string imagePath2, int targetWidth, int targetHeight){
        using (var image1 = Image.Load<Rgba32>(imagePath1))
        using (var image2 = Image.Load<Rgba32>(imagePath2)){
            // Preprocess images (resize and convert to grayscale)
            image1.Mutate(x => x.Resize(new ResizeOptions{
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Max
            }).Grayscale());

            image2.Mutate(x => x.Resize(new ResizeOptions{
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Max
            }).Grayscale());

            // Extract pixel values into arrays
            float[] pixels1 = ExtractPixels(image1, targetWidth, targetHeight);
            float[] pixels2 = ExtractPixels(image2, targetWidth, targetHeight);

            // Check if any frame is completely black, if so, skip SSIM calculation
            if (IsBlackFrame(pixels1) || IsBlackFrame(pixels2) ||
                IsMonochromaticFrame(pixels1) || IsMonochromaticFrame(pixels2)){
                // Return a negative value or zero to indicate no SSIM comparison for black frames.
                return (-1.0, 99);
            }

            // Compute SSIM
            return (CalculateSSIM(pixels1, pixels2), CalculatePixelDifference(pixels1, pixels2));
        }
    }

    private static double CalculatePixelDifference(float[] pixels1, float[] pixels2){
        double totalDifference = 0;
        int count = pixels1.Length;

        for (int i = 0; i < count; i++){
            totalDifference += Math.Abs(pixels1[i] - pixels2[i]);
        }

        return totalDifference / count; // Average difference
    }

    private static bool IsBlackFrame(float[] pixels, float threshold = 0.02f){
        // Check if all pixel values are below the threshold, indicating a black frame.
        return pixels.All(p => p <= threshold);
    }

    private static bool IsMonochromaticFrame(float[] pixels, float stdDevThreshold = 0.05f){
        float avg = pixels.Average();
        double variance = pixels.Average(p => Math.Pow(p - avg, 2));
        double stdDev = Math.Sqrt(variance);
        return stdDev < stdDevThreshold;
    }

    public static bool AreFramesSimilar(string imagePath1, string imagePath2, double ssimThreshold){
        var (ssim, pixelDiff) = ComputeSSIM(imagePath1, imagePath2, 256, 144);
        // Console.WriteLine($"SSIM: {ssim}");
        // Console.WriteLine(pixelDiff);

        return ssim > ssimThreshold && pixelDiff < 0.04;
    }

    public static float[] GetPixelsArray(string imagePath, int targetWidth = 256, int targetHeight = 144){
        using var image = Image.Load<Rgba32>(imagePath);
        image.Mutate(x => x.Resize(new ResizeOptions{
            Size = new Size(targetWidth, targetHeight),
            Mode = ResizeMode.Max
        }).Grayscale());
        return ExtractPixels(image, targetWidth, targetHeight);
    }

    public static bool AreFramesSimilarPreprocessed(float[] image1, float[] image2, double ssimThreshold){
        if (IsBlackFrame(image1) || IsBlackFrame(image2) ||
            IsMonochromaticFrame(image1) || IsMonochromaticFrame(image2)){
            return false;
        }

        var pixelDiff = CalculatePixelDifference(image1, image2);

        if (pixelDiff > 0.04){
            return false;
        }

        var ssim = CalculateSSIM(image1, image2);

        return ssim > ssimThreshold && pixelDiff < 0.04;
    }

    public static double CalculateOffset(List<FrameData> baseFrames, List<FrameData> compareFrames, bool reverseCompare = false, double ssimThreshold = 0.9){
        if (reverseCompare){
            baseFrames.Reverse();
            compareFrames.Reverse();
        }

        var preprocessedCompareFrames = compareFrames.Select(f => new{
            Frame = f,
            Pixels = GetPixelsArray(f.FilePath)
        }).ToList();

        var delay = 0.0;

        foreach (var baseFrame in baseFrames){
            var baseFramePixels = GetPixelsArray(baseFrame.FilePath);
            var matchingFrame = preprocessedCompareFrames.AsParallel()
                .WithExecutionMode(ParallelExecutionMode.ForceParallelism).FirstOrDefault(f => AreFramesSimilarPreprocessed(baseFramePixels, f.Pixels, ssimThreshold));
            if (matchingFrame != null){
                Console.WriteLine($"Matched Frame:");
                Console.WriteLine($"\t Base Frame Path: {baseFrame.FilePath} Time: {baseFrame.Time},");
                Console.WriteLine($"\t Compare Frame Path: {matchingFrame.Frame.FilePath} Time: {matchingFrame.Frame.Time}");
                delay = baseFrame.Time - matchingFrame.Frame.Time;
                break;
            } else{
                // Console.WriteLine($"No Match Found for Base Frame Time: {baseFrame.Time}");
                Debug.WriteLine($"No Match Found for Base Frame Time: {baseFrame.Time}");
            }
        }

        preprocessedCompareFrames.Clear();
        GC.Collect(); // Segment float arrays to avoid calling GC.Collect ?

        return delay;
    }
}