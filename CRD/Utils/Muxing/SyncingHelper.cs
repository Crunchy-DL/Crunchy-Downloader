using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CRD.Utils.Structs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace CRD.Utils.Muxing;

public class SyncingHelper{
    public static async Task<(bool IsOk, int ErrorCode, double frameRate)> ExtractFrames(string videoPath, string outputDir, double offset, double duration){
        var ffmpegPath = CfgManager.PathFFMPEG;
        var arguments = $"-i \"{videoPath}\" -vf \"select='gt(scene,0.1)',showinfo\" -vsync vfr -frame_pts true -t {duration} -ss {offset} \"{outputDir}\\frame%03d.png\"";

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
                        Console.WriteLine($"{e.Data}");
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

    private static double CalculateSSIM(float[] pixels1, float[] pixels2, int width, int height){
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

        double c1 = 0.01 * 0.01 * 255 * 255;
        double c2 = 0.03 * 0.03 * 255 * 255;

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
                    pixels[index++] = row[x].R;
                }
            }
        });

        return pixels;
    }

    public static double ComputeSSIM(string imagePath1, string imagePath2, int targetWidth, int targetHeight){
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

            // Compute SSIM
            return CalculateSSIM(pixels1, pixels2, targetWidth, targetHeight);
        }
    }

    public static bool AreFramesSimilar(string imagePath1, string imagePath2, double ssimThreshold){
        double ssim = ComputeSSIM(imagePath1, imagePath2, 256, 256);
        Console.WriteLine($"SSIM: {ssim}");
        return ssim > ssimThreshold;
    }

    public static double CalculateOffset(List<FrameData> baseFrames, List<FrameData> compareFrames, double ssimThreshold = 0.9){
        foreach (var baseFrame in baseFrames){
            var matchingFrame = compareFrames.FirstOrDefault(f => AreFramesSimilar(baseFrame.FilePath, f.FilePath, ssimThreshold));
            if (matchingFrame != null){
                Console.WriteLine($"Matched Frame: Base Frame Time: {baseFrame.Time}, Compare Frame Time: {matchingFrame.Time}");
                return baseFrame.Time - matchingFrame.Time;
            } else{
                // Console.WriteLine($"No Match Found for Base Frame Time: {baseFrame.Time}");
                Debug.WriteLine($"No Match Found for Base Frame Time: {baseFrame.Time}");
            }
        }

        return 0;
    }
}