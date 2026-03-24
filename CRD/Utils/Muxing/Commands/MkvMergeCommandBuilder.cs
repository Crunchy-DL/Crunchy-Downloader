using System;
using System.Linq;
using CRD.Utils.Muxing.Structs;
using CRD.Utils.Structs;

namespace CRD.Utils.Muxing.Commands;

public class MkvMergeCommandBuilder(MergerOptions options) : CommandBuilder(options){
    private bool hasVideo;

    public override string Build(){
        AddOutput();
        AddCustomOptions();
        AddVideoAudio();
        AddSubtitles();
        AddFonts();
        AddChapters();
        AddTitle();
        AddDescription();
        AddCover();

        return string.Join(" ", Args);
    }


    private void AddOutput(){
        Add($"-o \"{Helpers.AddUncPrefixIfNeeded(Options.Output)}\"");
    }

    private void AddCustomOptions(){
        if (Options.Options.Mkvmerge != null)
            AddRange(Options.Options.Mkvmerge);
    }

    private void AddVideoAudio(){
        if (Options.VideoAndAudio.Count > 0)
            AddCombinedVideoAudio();
        else{
            AddVideoOnly();
            AddAudioOnly();
        }
    }

    private void AddCombinedVideoAudio(){
        var rank = Options.DubLangList
            .Select((v, i) => new{ v, i })
            .ToDictionary(x => x.v, x => x.i, StringComparer.OrdinalIgnoreCase);

        var sorted = Options.VideoAndAudio
            .OrderBy(m => {
                var key = m.Language?.CrLocale ?? string.Empty;
                return rank.TryGetValue(key, out var r) ? r : int.MaxValue;
            })
            .ThenBy(m => m.IsAudioRoleDescription)
            .ToList();

        foreach (var track in sorted){
            AddCombinedTrack(track);
        }
    }

    private void AddCombinedTrack(MergerInput track){
        var videoTrackNum = "0";
        var audioTrackNum = "1";

        if (!hasVideo || Options.KeepAllVideos){
            Add($"--video-tracks {videoTrackNum}");
            Add($"--audio-tracks {audioTrackNum}");

            AddTrackMetadata(videoTrackNum, track.Language);

            hasVideo = true;
        } else{
            Add("--no-video");
            Add($"--audio-tracks {audioTrackNum}");
        }

        AddAudioMetadata(audioTrackNum, track);

        AddInput(track.Path);
    }

    private void AddVideoOnly(){
        foreach (var vid in Options.OnlyVid){
            if (!hasVideo || Options.KeepAllVideos){
                Add("--video-tracks 0");
                Add("--no-audio");

                AddTrackMetadata("0", vid.Language);

                hasVideo = true;

                AddInput(vid.Path);
            }
        }
    }

    private void AddAudioOnly(){
        var sorted = Options.OnlyAudio
            .OrderBy(a => {
                var index = Options.DubLangList.IndexOf(a.Language.CrLocale);
                return index != -1 ? index : int.MaxValue;
            })
            .ToList();

        foreach (var aud in sorted){
            Add("--audio-tracks 0");
            Add("--no-video");

            AddAudioMetadata("0", aud);

            if (aud.Delay is{ } delay && delay != 0)
                Add($"--sync 0:{delay}");

            AddInput(aud.Path);
        }
    }

    private void AddTrackMetadata(string trackNum, LanguageItem lang){
        Add($"--track-name {trackNum}:\"{lang.Name}\"");
        Add($"--language {trackNum}:{lang.Code}");
    }

    private void AddAudioMetadata(string trackNum, MergerInput track){
        var name = track.Language.Name +
                   (track.IsAudioRoleDescription ? " [AD]" : "");

        Add($"--track-name {trackNum}:\"{name}\"");
        Add($"--language {trackNum}:{track.Language.Code}");

        AddDefaultAudio(trackNum, track.Language);
    }

    private void AddDefaultAudio(string trackNum, LanguageItem lang){
        if (Options.Defaults.Audio?.Code == lang.Code &&
            Options.Defaults.Audio != Languages.DEFAULT_lang){
            Add($"--default-track {trackNum}");
        } else{
            Add($"--default-track {trackNum}:0");
        }
    }

    private void AddSubtitles(){
        if (Options.Subtitles.Count == 0 || Options.SkipSubMux){
            Add("--no-subtitles");
            return;
        }

        bool hasSignsSub =
            Options.Subtitles.Any(s => s.Signs &&
                                       Options.Defaults.Sub?.Code == s.Language.Code);

        var sorted = Options.Subtitles
            .OrderBy(s => Options.SubLangList.IndexOf(s.Language.CrLocale) != -1
                ? Options.SubLangList.IndexOf(s.Language.CrLocale)
                : int.MaxValue)
            .ThenBy(s => s.ClosedCaption ? 2 : s.Signs ? 1 : 0)
            .ToList();

        foreach (var sub in sorted)
            AddSubtitle(sub, hasSignsSub);
    }

    private void AddSubtitle(SubtitleInput subObj, bool hasSignsSub){
        bool isForced = false;

        AddIf(subObj.Delay.HasValue, $"--sync 0:{subObj.Delay}");

        string extra = subObj.ClosedCaption ? $" {Options.CcTag}" : "";
        extra += subObj.Signs ? " Signs" : "";

        string name = (subObj.Language.Language ?? subObj.Language.Name) + extra;

        Add($"--track-name 0:\"{name}\"");
        Add($"--language 0:\"{subObj.Language.Code}\"");

        AddSubtitleDefaults(subObj, hasSignsSub, ref isForced);

        if (subObj.ClosedCaption && Options.CcSubsMuxingFlag)
            Add("--hearing-impaired-flag 0:yes");

        if (subObj.Signs && Options.SignsSubsAsForced && !isForced)
            Add("--forced-track 0:yes");

        AddInput(subObj.File);
    }

    private void AddSubtitleDefaults(SubtitleInput subObj, bool hasSignsSub, ref bool isForced){
        if (Options.Defaults.Sub != null && Options.Defaults.Sub != Languages.DEFAULT_lang){
            if (Options.Defaults.Sub.Code == subObj.Language.Code &&
                (Options.DefaultSubSigns == subObj.Signs || Options.DefaultSubSigns && !hasSignsSub) &&
                subObj.ClosedCaption == false){
                Add("--default-track 0");

                if (Options.DefaultSubForcedDisplay){
                    Add("--forced-track 0:yes");
                    isForced = true;
                }
            } else{
                Add("--default-track 0:0");
            }
        } else{
            Add("--default-track 0:0");
        }
    }

    private void AddFonts(){
        if (Options.Fonts is not{ Count: > 0 }){
            Add("--no-attachments");
            return;
        }

        foreach (var font in Options.Fonts){
            Add($"--attachment-name \"{font.Name}\"");
            Add($"--attachment-mime-type \"{font.Mime}\"");
            Add($"--attach-file \"{Helpers.AddUncPrefixIfNeeded(font.Path)}\"");
        }
    }

    private void AddChapters(){
        if (Options.Chapters is{ Count: > 0 })
            Add($"--chapters \"{Helpers.AddUncPrefixIfNeeded(Options.Chapters[0].Path)}\"");
    }

    private void AddTitle(){
        if (!string.IsNullOrEmpty(Options.VideoTitle))
            Add($"--title \"{Options.VideoTitle}\"");
    }

    private void AddDescription(){
        if (Options.Description is{ Count: > 0 })
            Add($"--global-tags \"{Helpers.AddUncPrefixIfNeeded(Options.Description[0].Path)}\"");
    }

    private void AddCover(){
        var cover = Options.Cover.FirstOrDefault();

        if (cover?.Path != null){
            Add($"--attach-file \"{cover.Path}\"");
            Add("--attachment-mime-type image/png");
            Add("--attachment-name cover.png");
        }
    }
}