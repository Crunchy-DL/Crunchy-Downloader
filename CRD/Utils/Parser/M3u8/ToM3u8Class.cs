using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using CRD.Utils.Parser.Segments;
using CRD.Utils.Parser.Utils;

namespace CRD.Utils.Parser;

public class ToM3u8Class{
    public static dynamic ToM3u8(dynamic parsedPlaylists){
        List<dynamic> dashPlaylist = ObjectUtilities.GetMemberValue(parsedPlaylists, "dashPlaylist");
        dynamic locations = ObjectUtilities.GetMemberValue(parsedPlaylists, "locations");
        dynamic contentSteering = ObjectUtilities.GetMemberValue(parsedPlaylists, "contentSteering");
        dynamic sidxMapping = ObjectUtilities.GetMemberValue(parsedPlaylists, "sidxMapping");
        dynamic previousManifest = ObjectUtilities.GetMemberValue(parsedPlaylists, "previousManifest");
        dynamic eventStream = ObjectUtilities.GetMemberValue(parsedPlaylists, "eventStream");

        if (dashPlaylist == null || dashPlaylist.Count == 0){
            return new{ };
        }

        dynamic attributes = dashPlaylist[0].attributes;

        dynamic duration = ObjectUtilities.GetMemberValue(attributes, "sourceDuration");
        dynamic type = ObjectUtilities.GetMemberValue(attributes, "type");
        dynamic suggestedPresentationDelay = ObjectUtilities.GetMemberValue(attributes, "suggestedPresentationDelay");
        dynamic minimumUpdatePeriod = ObjectUtilities.GetMemberValue(attributes, "minimumUpdatePeriod");


        List<dynamic> videoPlaylists = MergeDiscontiguousPlaylists(dashPlaylist.FindAll(VideoOnly)).Select(FormatVideoPlaylist).ToList();
        List<dynamic> audioPlaylists = MergeDiscontiguousPlaylists(dashPlaylist.FindAll(AudioOnly));
        List<dynamic> vttPlaylists = MergeDiscontiguousPlaylists(dashPlaylist.FindAll(VttOnly));
        List<dynamic> captions = dashPlaylist
            .Select(playlist => ObjectUtilities.GetMemberValue(playlist.attributes, "captionServices"))
            .Where(captionService => captionService != null) // Filtering out null values
            .ToList();

        dynamic manifest = new ExpandoObject();
        manifest.allowCache = true;
        manifest.discontinuityStarts = new List<dynamic>();
        manifest.segments = new List<dynamic>();
        manifest.endList = true;
        manifest.mediaGroups = new ExpandoObject();
        manifest.mediaGroups.AUDIO = new ExpandoObject();
        manifest.mediaGroups.VIDEO = new ExpandoObject();
        manifest.mediaGroups.SUBTITLES = new ExpandoObject();
        manifest.uri = "";
        manifest.duration = duration;
        manifest.playlists = AddSidxSegmentsToPlaylists(videoPlaylists, sidxMapping);

        var mediaGroupsDict = (IDictionary<string, object>)manifest.mediaGroups;
        mediaGroupsDict["CLOSED-CAPTIONS"] = new ExpandoObject();

        if (minimumUpdatePeriod != null && minimumUpdatePeriod >= 0){
            manifest.minimumUpdatePeriod = minimumUpdatePeriod * 1000;
        }

        if (locations != null){
            manifest.locations = locations;
        }

        if (contentSteering != null){
            manifest.contentSteering = contentSteering;
        }

        if (type != null && type == "dynamic"){
            manifest.suggestedPresentationDelay = suggestedPresentationDelay;
        }

        if (eventStream != null && eventStream.Count > 0){
            manifest.eventStream = eventStream;
        }


        var isAudioOnly = ((List<dynamic>)manifest.playlists).Count == 0;
        var organizedAudioGroup = audioPlaylists.Count > 0 ? OrganizeAudioPlaylists(audioPlaylists, sidxMapping, isAudioOnly) : null;
        var organizedVttGroup = vttPlaylists.Count > 0 ? OrganizeVttPlaylists(vttPlaylists, sidxMapping) : null;

        List<dynamic> formattedPlaylists = new List<dynamic>(videoPlaylists);

        formattedPlaylists.AddRange(FlattenMediaGroupPlaylists(organizedAudioGroup));
        formattedPlaylists.AddRange(FlattenMediaGroupPlaylists(organizedVttGroup));


        dynamic playlistTimelineStarts = formattedPlaylists.Select(playlist => playlist.timelineStarts).ToList();

        List<List<dynamic>> convertedToList = new List<List<dynamic>>();
        foreach (var item in playlistTimelineStarts){
            if (item is List<dynamic>){
                convertedToList.Add(item);
            }
        }

        manifest.timelineStarts = PlaylistMerge.GetUniqueTimelineStarts(convertedToList);

        AddMediaSequenceValues(formattedPlaylists, manifest.timelineStarts);

        if (organizedAudioGroup != null){
            manifest.mediaGroups.AUDIO.audio = organizedAudioGroup;
        }

        if (organizedVttGroup != null){
            manifest.mediaGroups.SUBTITLES.subs = organizedVttGroup;
        }

        if (captions.Count > 0){
            dynamic closedCaptions = mediaGroupsDict["CLOSED-CAPTIONS"];
            closedCaptions.cc = OrganizeCaptionServices(captions);
        }

        if (previousManifest != null){
            return PlaylistMerge.PositionManifestOnTimeline(previousManifest, manifest);
        }

        return manifest;
    }

    public static bool VideoOnly(dynamic item){
        var attributes = item.attributes;
        return ObjectUtilities.GetMemberValue(attributes, "mimeType") == "video/mp4" || ObjectUtilities.GetMemberValue(attributes, "mimeType") == "video/webm" ||
               ObjectUtilities.GetMemberValue(attributes, "contentType") == "video";
    }

    public static bool AudioOnly(dynamic item){
        var attributes = item.attributes;
        return ObjectUtilities.GetMemberValue(attributes, "mimeType") == "audio/mp4" || ObjectUtilities.GetMemberValue(attributes, "mimeType") == "audio/webm" ||
               ObjectUtilities.GetMemberValue(attributes, "contentType") == "audio";
    }

    public static bool VttOnly(dynamic item){
        var attributes = item.attributes;
        return ObjectUtilities.GetMemberValue(attributes, "mimeType") == "text/vtt" || ObjectUtilities.GetMemberValue(attributes, "contentType") == "text";
    }

    public static dynamic FormatVideoPlaylist(dynamic item){
        dynamic playlist = new ExpandoObject();
        playlist.attributes = new ExpandoObject();
        playlist.attributes.NAME = item.attributes.id;
        playlist.attributes.AUDIO = "audio";
        playlist.attributes.SUBTITLES = "subs";
        playlist.attributes.RESOLUTION = new ExpandoObject();
        playlist.attributes.RESOLUTION.width = item.attributes.width;
        playlist.attributes.RESOLUTION.height = item.attributes.height;
        playlist.attributes.CODECS = item.attributes.codecs;
        playlist.attributes.BANDWIDTH = item.attributes.bandwidth;
        playlist.uri = "";
        playlist.endList = item.attributes.type == "static";
        playlist.timeline = item.attributes.periodStart;
        playlist.resolvedUri = item.attributes.baseUrl ?? "";
        playlist.targetDuration = item.attributes.duration;
        playlist.discontinuityStarts = item.discontinuityStarts;
        playlist.timelineStarts = item.attributes.timelineStarts;
        playlist.segments = item.segments;

        var attributesDict = (IDictionary<string, object>)playlist.attributes;
        attributesDict["PROGRAM-ID"] = 1;

        if (ObjectUtilities.GetMemberValue(item.attributes, "frameRate") != null){
            attributesDict["FRAME-RATE"] = item.attributes.frameRate;
        }

        if (ObjectUtilities.GetMemberValue(item.attributes, "contentProtection") != null){
            playlist.contentProtection = item.attributes.contentProtection;
        }

        if (ObjectUtilities.GetMemberValue(item.attributes, "serviceLocation") != null){
            playlist.attributes.serviceLocation = item.attributes.serviceLocation;
        }

        if (ObjectUtilities.GetMemberValue(item, "sidx") != null){
            playlist.sidx = item.sidx;
        }

        return playlist;
    }

    public static dynamic FormatAudioPlaylist(dynamic item, bool isAudioOnly){
        dynamic playlist = new ExpandoObject();
        playlist.attributes = new ExpandoObject();
        playlist.attributes.NAME = item.attributes.id;
        playlist.attributes.BANDWIDTH = item.attributes.bandwidth;
        playlist.attributes.AUDIOSAMPLINGRATE = item.attributes.audioSamplingRate;
        playlist.attributes.CODECS = item.attributes.codecs;
        playlist.uri = string.Empty;
        playlist.endList = item.attributes.type == "static";
        playlist.timeline = item.attributes.periodStart;
        playlist.resolvedUri = item.attributes.baseUrl ?? string.Empty;
        playlist.targetDuration = item.attributes.duration;
        playlist.discontinuitySequence = ObjectUtilities.GetMemberValue(item, "discontinuitySequence");
        playlist.discontinuityStarts = item.discontinuityStarts;
        playlist.timelineStarts = item.attributes.timelineStarts;
        playlist.mediaSequence = ObjectUtilities.GetMemberValue(item, "mediaSequence");
        playlist.segments = item.segments;

        var attributesDict = (IDictionary<string, object>)playlist.attributes;
        attributesDict["PROGRAM-ID"] = 1;

        if (ObjectUtilities.GetMemberValue(item.attributes, "contentProtection") != null){
            playlist.contentProtection = item.attributes.contentProtection;
        }

        if (ObjectUtilities.GetMemberValue(item.attributes, "serviceLocation") != null){
            playlist.attributes.serviceLocation = item.attributes.serviceLocation;
        }

        if (ObjectUtilities.GetMemberValue(item, "sidx") != null){
            playlist.sidx = item.sidx;
        }

        if (isAudioOnly){
            playlist.attributes.AUDIO = "audio";
            playlist.attributes.SUBTITLES = "subs";
        }

        return playlist;
    }

    public static dynamic FormatVttPlaylist(dynamic item){
        if (ObjectUtilities.GetMemberValue(item,"segments") == null){
            // VTT tracks may use a single file in BaseURL
            var segment = new ExpandoObject() as IDictionary<string, object>;
            segment["uri"] = item.attributes.baseUrl;
            segment["timeline"] = item.attributes.periodStart;
            segment["resolvedUri"] = item.attributes.baseUrl ?? string.Empty;
            segment["duration"] = item.attributes.sourceDuration;
            segment["number"] = 0;

            item.segments = new List<dynamic>{ segment };

            // TargetDuration should be the same duration as the only segment
            item.attributes.duration = item.attributes.sourceDuration;
        }

        var m3u8Attributes = new ExpandoObject() as IDictionary<string, object>;
        m3u8Attributes["NAME"] = item.attributes.id;
        m3u8Attributes["BANDWIDTH"] = item.attributes.bandwidth;
        m3u8Attributes["PROGRAM-ID"] = 1;

        
        
        if (ObjectUtilities.GetMemberValue(item.attributes,"codecs") != null){
            m3u8Attributes["CODECS"] = item.attributes.codecs;
        }

        dynamic vttPlaylist = new ExpandoObject();
        vttPlaylist.attributes = m3u8Attributes;
        vttPlaylist.uri = string.Empty;
        vttPlaylist.endList = item.attributes.type == "static";
        vttPlaylist.timeline = item.attributes.periodStart;
        vttPlaylist.resolvedUri = item.attributes.baseUrl ?? string.Empty;
        vttPlaylist.targetDuration = item.attributes.duration;
        vttPlaylist.timelineStarts = item.attributes.timelineStarts;
        vttPlaylist.discontinuityStarts = item.discontinuityStarts;
        vttPlaylist.discontinuitySequence = ObjectUtilities.GetMemberValue(item, "discontinuitySequence");
        vttPlaylist.mediaSequence = ObjectUtilities.GetMemberValue(item,"mediaSequence");
        vttPlaylist.segments = item.segments;

        if (ObjectUtilities.GetMemberValue(item.attributes, "serviceLocation") != null){
            vttPlaylist.attributes.serviceLocation = item.attributes.serviceLocation;
        }

        return vttPlaylist;
    }

    public static dynamic OrganizeCaptionServices(List<dynamic> captionServices){
        var svcObj = new ExpandoObject() as IDictionary<string, object>;

        foreach (var svc in captionServices){
            if (svc == null) continue;

            foreach (var service in svc){
                string channel = service.channel;
                string language = service.language;

                var serviceDetails = new ExpandoObject() as IDictionary<string, object>;
                serviceDetails["autoselect"] = false;
                serviceDetails["default"] = false;
                serviceDetails["instreamId"] = channel;
                serviceDetails["language"] = language;

                // Optionally add properties if they exist
                if (((IDictionary<string, object>)service).ContainsKey("aspectRatio")){
                    serviceDetails["aspectRatio"] = service.aspectRatio;
                }

                if (((IDictionary<string, object>)service).ContainsKey("easyReader")){
                    serviceDetails["easyReader"] = service.easyReader;
                }

                if (((IDictionary<string, object>)service).ContainsKey("3D")){
                    serviceDetails["3D"] = service["3D"];
                }

                svcObj[language] = serviceDetails;
            }
        }

        return svcObj;
    }

    public static List<dynamic> FlattenMediaGroupPlaylists(dynamic mediaGroupObject){
        if (mediaGroupObject == null) return new List<dynamic>();

        var result = new List<dynamic>();
        foreach (var key in ((IDictionary<string, dynamic>)mediaGroupObject).Keys){
            var labelContents = mediaGroupObject[key];
            if (labelContents.playlists != null && labelContents.playlists is List<dynamic>){
                result.AddRange(labelContents.playlists);
            }
        }

        return result;
    }

    
    public static List<dynamic> MergeDiscontiguousPlaylists(List<dynamic> playlists){
        // Break out playlists into groups based on their baseUrl
        var playlistsByBaseUrl = playlists.GroupBy(
                p => p.attributes.baseUrl,
                p => p,
                (key, g) => new{ BaseUrl = key, Playlists = g.ToList() })
            .ToDictionary(g => g.BaseUrl, g => g.Playlists);

        var allPlaylists = new List<dynamic>();

        foreach (var playlistGroup in playlistsByBaseUrl.Values){
            var mergedPlaylists = playlistGroup
                .GroupBy(
                    p => p.attributes.id + (ObjectUtilities.GetMemberValue(p.attributes, "lang") ?? ""),
                    p => p,
                    (key, g) => new{ Name = key, Playlists = g.ToList() })
                .Select(g => {
                    dynamic mergedPlaylist = new ExpandoObject();
                    mergedPlaylist.attributes = new ExpandoObject();
                    mergedPlaylist.attributes.timelineStarts = new List<dynamic>();

                    foreach (var playlist in g.Playlists){
                        if (ObjectUtilities.GetMemberValue(mergedPlaylist, "segments") == null){
                            mergedPlaylist = playlist;
                            mergedPlaylist.attributes.timelineStarts = new List<dynamic>();
                        } else{
                            if (playlist.segments != null && playlist.segments.Count > 0){
                                playlist.segments[0].discontinuity = true;
                                foreach (var segment in playlist.segments){
                                    mergedPlaylist.segments.Add(segment);
                                }
                            }

                            if (playlist.attributes.contentProtection != null){
                                mergedPlaylist.attributes.contentProtection = playlist.attributes.contentProtection;
                            }
                        }

                        mergedPlaylist.attributes.timelineStarts.Add(new{
                            start = playlist.attributes.periodStart,
                            timeline = playlist.attributes.periodStart
                        });
                    }

                    return mergedPlaylist;
                })
                .ToList();

            allPlaylists.AddRange(mergedPlaylists);
        }

        return allPlaylists.Select(playlist => {
            playlist.discontinuityStarts = FindIndexes((List<dynamic>) ObjectUtilities.GetMemberValue(playlists,"segments") ?? new List<dynamic>(), "discontinuity");
            return playlist;
        }).ToList();
    }

    public static IDictionary<string, dynamic> OrganizeAudioPlaylists(List<dynamic> playlists, IDictionary<string, dynamic>? sidxMapping = null, bool isAudioOnly = false){
        sidxMapping ??= new Dictionary<string, dynamic>(); // Ensure sidxMapping is not null
        dynamic mainPlaylist = null;

        var formattedPlaylists = playlists.Aggregate(new Dictionary<string, dynamic>(), (acc, playlist) => {
            var role = ObjectUtilities.GetMemberValue(playlist.attributes, "role") != null && ObjectUtilities.GetMemberValue(playlist.attributes.role, "value") != null ? playlist.attributes.role.value : string.Empty;
            var language = ObjectUtilities.GetMemberValue(playlist.attributes, "lang") ?? string.Empty;

            var label = ObjectUtilities.GetMemberValue(playlist.attributes, "label") ?? "main";
            if (!string.IsNullOrEmpty(language) && string.IsNullOrEmpty(label)){
                var roleLabel = !string.IsNullOrEmpty(role) ? $" ({role})" : string.Empty;
                label = $"{language}{roleLabel}";
            }

            if (!acc.ContainsKey(label)){
                acc[label] = new ExpandoObject();
                acc[label].language = language;
                acc[label].autoselect = true;
                acc[label].@default = role == "main";
                acc[label].playlists = new List<dynamic>();
                acc[label].uri = string.Empty;
            }

            var formatted = AddSidxSegmentsToPlaylist(FormatAudioPlaylist(playlist, isAudioOnly), sidxMapping);
            acc[label].playlists.Add(formatted);

            if (mainPlaylist == null && role == "main"){
                mainPlaylist = playlist;
                mainPlaylist.@default = true; // Use '@' to escape reserved keyword
            }

            return acc;
        });

        // If no playlists have role "main", mark the first as main
        if (mainPlaylist == null && formattedPlaylists.Count > 0){
            var firstLabel = formattedPlaylists.Keys.First();
            formattedPlaylists[firstLabel].@default = true; // Use '@' to escape reserved keyword
        }

        return formattedPlaylists;
    }

    public static IDictionary<string, dynamic> OrganizeVttPlaylists(List<dynamic> playlists, IDictionary<string, dynamic>? sidxMapping = null){
        sidxMapping ??= new Dictionary<string, dynamic>(); // Ensure sidxMapping is not null

        var organizedPlaylists = playlists.Aggregate(new Dictionary<string, dynamic>(), (acc, playlist) => {
            var label = playlist.attributes.label ?? playlist.attributes.lang ?? "text";

            if (!acc.ContainsKey(label)){
                dynamic playlistGroup = new ExpandoObject();
                playlistGroup.language = label;
                playlistGroup.@default = false; // '@' is used to escape C# keyword
                playlistGroup.autoselect = false;
                playlistGroup.playlists = new List<dynamic>();
                playlistGroup.uri = string.Empty;

                acc[label] = playlistGroup;
            }

            acc[label].playlists.Add(AddSidxSegmentsToPlaylist(FormatVttPlaylist(playlist), sidxMapping));

            return acc;
        });

        return organizedPlaylists;
    }


    public static void AddMediaSequenceValues(List<dynamic> playlists, List<dynamic> timelineStarts){
        foreach (var playlist in playlists){
            playlist.mediaSequence = 0;
            playlist.discontinuitySequence = timelineStarts.FindIndex(ts => ts.timeline == playlist.timeline);

            if (playlist.segments == null) continue;

            for (int i = 0; i < playlist.segments.Count; i++){
                playlist.segments[i].number = i;
            }
        }
    }

    public static List<int> FindIndexes(List<dynamic> list, string key){
        var indexes = new List<int>();
        for (int i = 0; i < list.Count; i++){
            var expandoDict = list[i] as IDictionary<string, object>;
            if (expandoDict != null && expandoDict.ContainsKey(key) && expandoDict[key] != null){
                indexes.Add(i);
            }
        }

        return indexes;
    }

    public static dynamic AddSidxSegmentsToPlaylist(dynamic playlist, IDictionary<string, dynamic> sidxMapping){
        string sidxKey = GenerateSidxKey(ObjectUtilities.GetMemberValue(playlist, "sidx"));
        if (!string.IsNullOrEmpty(sidxKey) && sidxMapping.ContainsKey(sidxKey)){
            var sidxMatch = sidxMapping[sidxKey];
            if (sidxMatch != null){
                SegmentBase.AddSidxSegmentsToPlaylist(playlist, sidxMatch.sidx, playlist.sidx.resolvedUri);
            }
        }

        return playlist;
    }

    public static List<dynamic> AddSidxSegmentsToPlaylists(List<dynamic> playlists, IDictionary<string, dynamic>? sidxMapping = null){
        sidxMapping ??= new Dictionary<string, dynamic>();

        if (sidxMapping.Count == 0){
            return playlists;
        }

        for (int i = 0; i < playlists.Count; i++){
            playlists[i] = AddSidxSegmentsToPlaylist(playlists[i], sidxMapping);
        }

        return playlists;
    }

    public static string GenerateSidxKey(dynamic sidx){
        return sidx != null ? $"{sidx.uri}-{UrlType.ByteRangeToString(sidx.byterange)}" : null;
    }
}