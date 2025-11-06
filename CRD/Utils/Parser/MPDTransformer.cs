using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CRD.Utils.DRM;
using CRD.Utils.HLS;
using CRD.Utils.Parser;
using CRD.Utils.Parser.Utils;
using CRD.Utils.Structs;

namespace CRD.Utils;

public class Segment{
    public string uri{ get; set; }
    public double timeline{ get; set; }
    public double duration{ get; set; }
    public Map map{ get; set; }
    
    public ByteRange? byteRange { get; set; }
    public double? number{ get; set; }
    public double? presentationTime{ get; set; }
}

public class Map{
    public string uri { get; set; }
    
    public ByteRange? byteRange { get; set; }
}

public class PlaylistItem{
    public string? pssh{ get; set; }
    public List<ContentKey> encryptionKeys{ get; set; } =[];
    public int bandwidth{ get; set; }
    public List<Segment> segments{ get; set; }
}

public class AudioPlaylist : PlaylistItem{
    public LanguageItem? language{ get; set; }
    
    public int audioSamplingRate{ get; set; }
    public bool @default{ get; set; }
}

public class VideoPlaylist : PlaylistItem{
    public Quality quality{ get; set; }
}

public class  VideoItem: VideoPlaylist{
    public string resolutionText{ get; set; }
}

public class  AudioItem: AudioPlaylist{
    public string resolutionText{ get; set; }
    public string resolutionTextSnap{ get; set; }
}

public class Quality{
    public int width{ get; set; }
    public int height{ get; set; }
}

public class MPDParsed{
    public Dictionary<string, ServerData> Data{ get; set; }
}

public class ServerData{
    public List<AudioPlaylist> audio{ get; set; } =[];
    public List<VideoPlaylist> video{ get; set; } =[];
}

public static class MPDParser{
    public static MPDParsed Parse(string manifest, LanguageItem? language, string? url){
        if (!manifest.Contains("BaseURL") && url != null){
            XDocument doc = XDocument.Parse(manifest);
            XElement mpd = doc.Element("MPD");
            mpd.AddFirst(new XElement("BaseURL", url));
            manifest = doc.ToString();
        }

        dynamic parsed = DashParser.Parse(manifest);

        MPDParsed ret = new MPDParsed{ Data = new Dictionary<string, ServerData>() };

        foreach (var item in parsed.mediaGroups.AUDIO.audio.Values){
            foreach (var playlist in item.playlists){
                var host = new Uri(playlist.resolvedUri).Host;
                EnsureHostEntryExists(ret, host);

                List<dynamic> segments = playlist.segments;

                if (ObjectUtilities.GetMemberValue(playlist,"sidx") != null && segments.Count == 0){
                    throw new NotImplementedException();
                }
                
                var foundLanguage = Languages.FindLang(Languages.languages.FirstOrDefault(a => a.CrLocale == item.language)?.CrLocale ?? "unknown");
                LanguageItem? audioLang = item.language != null ? foundLanguage : (language != null ? language : foundLanguage);

                var pItem = new AudioPlaylist{
                    bandwidth = playlist.attributes.BANDWIDTH,
                    audioSamplingRate = ObjectUtilities.GetMemberValue(playlist.attributes ,"AUDIOSAMPLINGRATE") ?? 0,
                    language = audioLang,
                    @default = item.@default,
                    segments = segments.Select(segment => new Segment{
                        duration = segment.duration,
                        map = new Map{uri = segment.map.resolvedUri,byteRange = ObjectUtilities.GetMemberValue(segment.map,"byterange")},
                        number = segment.number,
                        presentationTime = segment.presentationTime,
                        timeline = segment.timeline,
                        uri = segment.resolvedUri,
                        byteRange = ObjectUtilities.GetMemberValue(segment,"byterange")
                    }).ToList()
                };

                var contentProtectionDict = (IDictionary<string, dynamic>)ObjectUtilities.GetMemberValue(playlist,"contentProtection");
                
                if (contentProtectionDict != null && contentProtectionDict.ContainsKey("com.widevine.alpha")  && contentProtectionDict["com.widevine.alpha"].pssh != null)
                    pItem.pssh = ArrayBufferToBase64(contentProtectionDict["com.widevine.alpha"].pssh);

                ret.Data[host].audio.Add(pItem);
            }
        }

        foreach (var playlist in parsed.playlists){
            var host = new Uri(playlist.resolvedUri).Host;
            EnsureHostEntryExists(ret, host);
        
            List<dynamic> segments = playlist.segments;

            if (ObjectUtilities.GetMemberValue(playlist,"sidx") != null && segments.Count == 0){
                throw new NotImplementedException();
            }

            
            dynamic resolution = ObjectUtilities.GetMemberValue(playlist.attributes,"RESOLUTION");
            resolution = resolution != null ? resolution : new Quality();
            
            var pItem = new VideoPlaylist{
                bandwidth = playlist.attributes.BANDWIDTH,
                quality = new Quality{height = resolution.height,width = resolution.width},
                segments = segments.Select(segment => new Segment{
                    duration = segment.duration,
                    map = new Map{uri = segment.map.resolvedUri,byteRange = ObjectUtilities.GetMemberValue(segment.map,"byterange")},
                    number = segment.number,
                    presentationTime = segment.presentationTime,
                    timeline = segment.timeline,
                    uri = segment.resolvedUri,
                    byteRange = ObjectUtilities.GetMemberValue(segment,"byterange")
                }).ToList()
            };
        
            var contentProtectionDict = (IDictionary<string, dynamic>)ObjectUtilities.GetMemberValue(playlist,"contentProtection");
                
            if (contentProtectionDict != null && contentProtectionDict.ContainsKey("com.widevine.alpha")  && contentProtectionDict["com.widevine.alpha"].pssh != null)
                pItem.pssh = ArrayBufferToBase64(contentProtectionDict["com.widevine.alpha"].pssh);

        
            ret.Data[host].video.Add(pItem);
        }

        return ret;
    }

    private static void EnsureHostEntryExists(MPDParsed ret, string host){
        if (!ret.Data.ContainsKey(host)){
            ret.Data[host] = new ServerData{ audio = new List<AudioPlaylist>(), video = new List<VideoPlaylist>() };
        }
    }

    public static string ArrayBufferToBase64(byte[] buffer){
        return Convert.ToBase64String(buffer);
    }
}