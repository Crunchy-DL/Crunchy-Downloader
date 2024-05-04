using System;
using System.Collections.Generic;
using System.Linq;

namespace CRD.Utils.Parser;

public class PlaylistMerge{
    public static List<dynamic> Union(List<List<dynamic>> lists, Func<dynamic, dynamic> keyFunction){
        var uniqueElements = new Dictionary<dynamic, dynamic>();

        foreach (var list in lists){
            foreach (var element in list){
                dynamic key = keyFunction(element);
                if (!uniqueElements.ContainsKey(key)){
                    uniqueElements[key] = element;
                }
            }
        }

        // Return the values as a list
        return uniqueElements.Values.ToList();
    }

    public static List<dynamic> GetUniqueTimelineStarts(List<List<dynamic>> timelineStarts){
        var uniqueStarts = Union(timelineStarts, el => el.timeline);

        // Sort the results based on the timeline
        return uniqueStarts.OrderBy(el => el.timeline).ToList();
    }

    public static dynamic PositionManifestOnTimeline(dynamic oldManifest, dynamic newManifest){
        List<dynamic> oldPlaylists = ((List<dynamic>)oldManifest.playlists).AddRange(GetMediaGroupPlaylists(oldManifest)).ToList();
        List<dynamic>  newPlaylists = ((List<dynamic>)newManifest.playlists).AddRange(GetMediaGroupPlaylists(newManifest)).ToList();

        newManifest.timelineStarts = GetUniqueTimelineStarts(new List<List<dynamic>>{ oldManifest.timelineStarts, newManifest.timelineStarts });

        // Assuming UpdateSequenceNumbers is implemented elsewhere
        UpdateSequenceNumbers(oldPlaylists, newPlaylists, newManifest.timelineStarts);

        return newManifest;
    }

    private static readonly string[] SupportedMediaTypes ={ "AUDIO", "SUBTITLES" };

    public static List<dynamic> GetMediaGroupPlaylists(dynamic manifest){
        var mediaGroupPlaylists = new List<dynamic>();

        foreach (var mediaType in SupportedMediaTypes){
            var mediaGroups = (IDictionary<string, object>)manifest.mediaGroups[mediaType];
            foreach (var groupKey in mediaGroups.Keys){
                var labels = (IDictionary<string, object>)mediaGroups[groupKey];
                foreach (var labelKey in labels.Keys){
                    var properties = (dynamic)labels[labelKey];
                    if (properties.playlists != null){
                        mediaGroupPlaylists.AddRange(properties.playlists);
                    }
                }
            }
        }

        return mediaGroupPlaylists;
    }

    private const double TimeFudge = 1 / (double)60;

    public static void UpdateSequenceNumbers(List<dynamic> oldPlaylists, List<dynamic> newPlaylists, List<dynamic> timelineStarts){
        foreach (dynamic playlist in newPlaylists){
            playlist.discontinuitySequence = timelineStarts.FindIndex(ts => ts.timeline == playlist.timeline);

            dynamic oldPlaylist = FindPlaylistWithName(oldPlaylists, playlist.attributes.NAME);

            if (oldPlaylist == null){
                // New playlist, no further processing needed
                continue;
            }

            if (playlist.sidx != null){
                // Skip playlists with sidx
                continue;
            }

            if (!playlist.segments.Any()){
                // No segments to process
                continue;
            }

            dynamic firstNewSegment = playlist.segments[0];
            List<dynamic> segmentList = oldPlaylist.segments;
            dynamic oldMatchingSegmentIndex = segmentList.FindIndex(
                oldSegment => Math.Abs(oldSegment.presentationTime - firstNewSegment.presentationTime) < TimeFudge
            );

            if (oldMatchingSegmentIndex == -1){
                UpdateMediaSequenceForPlaylist(playlist, oldPlaylist.mediaSequence + oldPlaylist.segments.Count);
                playlist.segments[0].discontinuity = true;
                playlist.discontinuityStarts.Insert(0, 0);

                if ((!oldPlaylist.segments.Any() && playlist.timeline > oldPlaylist.timeline) ||
                    (oldPlaylist.segments.Any() && playlist.timeline > oldPlaylist.segments.Last().timeline)){
                    playlist.discontinuitySequence--;
                }

                continue;
            }

            var oldMatchingSegment = oldPlaylist.segments[oldMatchingSegmentIndex];

            if (oldMatchingSegment.discontinuity && !firstNewSegment.discontinuity){
                firstNewSegment.discontinuity = true;
                playlist.discontinuityStarts.Insert(0, 0);
                playlist.discontinuitySequence--;
            }

            UpdateMediaSequenceForPlaylist(playlist, oldPlaylist.segments[oldMatchingSegmentIndex].number);
        }
    }

    public static dynamic FindPlaylistWithName(List<dynamic> playlists, string name){
        return playlists.FirstOrDefault(playlist => playlist.attributes.NAME == name);
    }

    public static void UpdateMediaSequenceForPlaylist(dynamic playlist, int mediaSequence){
        playlist.mediaSequence = mediaSequence;

        if (playlist.segments == null) return;

        for (int index = 0; index < playlist.segments.Count; index++){
            playlist.segments[index].number = playlist.mediaSequence + index;
        }
    }
}