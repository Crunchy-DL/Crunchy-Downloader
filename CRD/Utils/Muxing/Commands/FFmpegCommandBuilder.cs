using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using CRD.Utils.Muxing.Structs;
using CRD.Utils.Structs;

namespace CRD.Utils.Muxing.Commands;

public class FFmpegCommandBuilder : CommandBuilder{
    private readonly List<string> metaData = new();

    private int index;
    private int audioIndex;
    private bool hasVideo;

    public FFmpegCommandBuilder(MergerOptions options) : base(options){
    }

    public override string Build(){
        AddLogLevel();

        if (!Options.mp3)
            BuildMux();
        else
            BuildMp3();

        return string.Join(" ", Args);
    }

    private void AddLogLevel(){
        Add("-loglevel warning");
    }

    private void BuildMux(){
        AddVideoInputs();
        AddAudioInputs();
        AddChapterInput();
        AddSubtitleInputs();

        AddRange(metaData);

        AddCodecs();
        AddSubtitleMetadata();
        AddGlobalMetadata();
        AddCustomOptions();
        AddOutput();
    }

    private void AddVideoInputs(){
        foreach (var vid in Options.OnlyVid){
            if (!hasVideo || Options.KeepAllVideos){
                Add($"-i \"{vid.Path}\"");

                metaData.Add($"-map {index}:v");
                metaData.Add($"-metadata:s:v:{index} title=\"{vid.Language.Name}\"");

                hasVideo = true;
                index++;
            }
        }
    }

    private void AddAudioInputs(){
        foreach (var aud in Options.OnlyAudio){
            if (aud.Delay is{ } delay && delay != 0){
                double offset = delay / 1000.0;
                Add($"-itsoffset {offset.ToString(CultureInfo.InvariantCulture)}");
            }

            Add($"-i \"{aud.Path}\"");

            metaData.Add($"-map {index}:a");
            metaData.Add($"-metadata:s:a:{audioIndex} language={aud.Language.Code}");

            AddAudioDisposition(aud);

            index++;
            audioIndex++;
        }
    }

    private void AddAudioDisposition(MergerInput aud){
        if (Options.Defaults.Audio?.Code == aud.Language.Code &&
            Options.Defaults.Audio != Languages.DEFAULT_lang){
            metaData.Add($"-disposition:a:{audioIndex} default");
        } else{
            metaData.Add($"-disposition:a:{audioIndex} 0");
        }
    }

    private void AddChapterInput(){
        if (Options.Chapters is{ Count: > 0 }){
            ConvertChapterFileForFFMPEG(Options.Chapters[0].Path);

            Add($"-i \"{Options.Chapters[0].Path}\"");

            metaData.Add($"-map_metadata {index}");

            index++;
        }
    }

    private void AddSubtitleInputs(){
        if (Options.SkipSubMux)
            return;

        bool hasSignsSub =
            Options.Subtitles.Any(s =>
                s.Signs && Options.Defaults.Sub?.Code == s.Language.Code);

        foreach (var sub in Options.Subtitles.Select((value, i) => new{ value, i })){
            AddSubtitle(sub.value, sub.i, hasSignsSub);
        }
    }

    private void AddSubtitle(SubtitleInput sub, int subIndex, bool hasSignsSub){
        if (sub.Delay is{ } delay && delay != 0){
            double offset = delay / 1000.0;
            Add($"-itsoffset {offset.ToString(CultureInfo.InvariantCulture)}");
        }

        Add($"-i \"{sub.File}\"");

        metaData.Add($"-map {index}:s");

        if (Options.Defaults.Sub?.Code == sub.Language.Code &&
            (Options.DefaultSubSigns == sub.Signs || Options.DefaultSubSigns && !hasSignsSub) &&
            !sub.ClosedCaption){
            metaData.Add($"-disposition:s:{subIndex} default");
        } else{
            metaData.Add($"-disposition:s:{subIndex} 0");
        }

        index++;
    }

    private void AddCodecs(){
        Add("-c:v copy");
        Add("-c:a copy");

        Add(
            Options.Output.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? "-c:s mov_text"
                : "-c:s ass"
        );
    }

    private void AddSubtitleMetadata(){
        if (Options.SkipSubMux)
            return;

        AddRange(
            Options.Subtitles.Select((sub, subIndex) =>
                $"-metadata:s:s:{subIndex} title=\"{sub.Language.Language ?? sub.Language.Name}" +
                $"{(sub.ClosedCaption ? $" {Options.CcTag}" : "")}" +
                $"{(sub.Signs ? " Signs" : "")}\" " +
                $"-metadata:s:s:{subIndex} language={sub.Language.Code}"
            )
        );
    }

    private void AddGlobalMetadata(){
        if (!string.IsNullOrEmpty(Options.VideoTitle))
            Add($"-metadata title=\"{Options.VideoTitle}\"");

        if (Options.Description is{ Count: > 0 }){
            XmlDocument doc = new();
            doc.Load(Options.Description[0].Path);

            XmlNode? node =
                doc.SelectSingleNode("//Tag/Simple[Name='DESCRIPTION']/String");

            string description =
                node?.InnerText
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                ?? string.Empty;

            Add($"-metadata comment=\"{description}\"");
        }
    }

    private void AddCustomOptions(){
        if (Options.Options.Ffmpeg?.Count > 0)
            AddRange(Options.Options.Ffmpeg);
    }

    private void AddOutput(){
        Add($"\"{Options.Output}\"");
    }

    private void BuildMp3(){
        if (Options.OnlyAudio.Count > 1)
            Console.Error.WriteLine(
                "Multiple audio files detected. Only one audio file can be converted to MP3 at a time."
            );

        var audio = Options.OnlyAudio.First();

        Add($"-i \"{audio.Path}\"");

        Add("-c:a libmp3lame" +
            (audio.Bitrate > 0 ? $" -b:a {audio.Bitrate}k" : ""));

        Add($"\"{Options.Output}\"");
    }


    private void ConvertChapterFileForFFMPEG(string chapterFilePath){
        var chapterLines = File.ReadAllLines(chapterFilePath);
        var ffmpegChapterLines = new List<string>{ ";FFMETADATA1" };
        var chapters = new List<(double StartTime, string Title)>();

        for (int i = 0; i < chapterLines.Length; i += 2){
            var timeLine = chapterLines[i];
            var nameLine = chapterLines[i + 1];

            var timeParts = timeLine.Split('=');
            var nameParts = nameLine.Split('=');

            if (timeParts.Length == 2 && nameParts.Length == 2){
                var startTime = TimeSpan.Parse(timeParts[1]).TotalMilliseconds;
                var title = nameParts[1];
                chapters.Add((startTime, title));
            }
        }

        // Sort chapters by start time
        chapters = chapters.OrderBy(c => c.StartTime).ToList();

        for (int i = 0; i < chapters.Count; i++){
            var startTime = chapters[i].StartTime;
            var title = chapters[i].Title;
            var endTime = (i + 1 < chapters.Count) ? chapters[i + 1].StartTime : startTime + 10000; // Add 10 seconds to the last chapter end time

            if (endTime < startTime){
                endTime = startTime + 10000; // Correct end time if it is before start time
            }

            ffmpegChapterLines.Add("[CHAPTER]");
            ffmpegChapterLines.Add("TIMEBASE=1/1000");
            ffmpegChapterLines.Add($"START={startTime}");
            ffmpegChapterLines.Add($"END={endTime}");
            ffmpegChapterLines.Add($"title={title}");
        }

        File.WriteAllLines(chapterFilePath, ffmpegChapterLines);
    }
}