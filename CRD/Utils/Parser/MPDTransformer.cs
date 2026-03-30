using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using CRD.Utils.DRM;
using CRD.Utils.HLS;
using CRD.Utils.Http;
using CRD.Utils.Parser.Utils;
using CRD.Utils.Structs;

namespace CRD.Utils.Parser;

public class Segment{
    public string uri{ get; set; }
    public double timeline{ get; set; }
    public double duration{ get; set; }
    public Map map{ get; set; }

    public ByteRange? byteRange{ get; set; }
    public double? number{ get; set; }
    public double? presentationTime{ get; set; }
}

public class Map{
    public string uri{ get; set; }

    public ByteRange? byteRange{ get; set; }
}

public class PlaylistItem{
    public string? pssh{ get; set; }

    public List<ContentKey> encryptionKeys{ get; set; } = [];
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
    public string codecs{ get; set; }
}

public class VideoItem : VideoPlaylist{
    public string resolutionText{ get; set; }
}

public class AudioItem : AudioPlaylist{
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
    public List<string> servers{ get; set; } = [];
    public List<AudioPlaylist>? audio{ get; set; } = [];
    public List<VideoPlaylist>? video{ get; set; } = [];
}

public static class MpdParser{
    public async static Task<MPDParsed> Parse(string manifest, LanguageItem? language, string? url){
        if (!manifest.Contains("BaseURL") && url != null){
            XDocument doc = XDocument.Parse(manifest);
            XElement? mpd = doc.Element("MPD");
            mpd?.AddFirst(new XElement("BaseURL", url));
            manifest = doc.ToString();
        }

        dynamic parsed = DashParser.Parse(manifest);

        MPDParsed ret = new MPDParsed{ Data = new Dictionary<string, ServerData>() };

        foreach (var item in parsed.mediaGroups.AUDIO.audio.Values){
            foreach (var playlist in item.playlists){
                var uri = new Uri(playlist.resolvedUri);
                var host = uri.Host;

                EnsureHostEntryExists(ret, host);

                List<dynamic> segments = playlist.segments;
                List<Segment>? segmentsFromSidx = null;

                if (ObjectUtilities.GetMemberValue(playlist, "sidx") != null){
                    if (segments == null || segments.Count == 0){
                        var sidxRange = playlist.sidx.ByteRange;

                        var sidxBytes = await DownloadSidxAsync(
                            HttpClientReq.Instance.GetHttpClient(),
                            playlist.sidx.uri,
                            sidxRange.offset,
                            sidxRange.offset + sidxRange.length - 1);

                        var sidx = ParseSidx(sidxBytes, sidxRange.offset);

                        var byteRange = new ByteRange(){
                            Length = playlist.sidx.map.ByteRange.length,
                            Offset = playlist.sidx.map.ByteRange.offset,
                        };

                        segmentsFromSidx = BuildSegmentsFromSidx(
                            sidx,
                            playlist.resolvedUri,
                            byteRange);
                    }
                }
                

                var foundLanguage =
                    Languages.FindLang(
                        Languages.languages.FirstOrDefault(a => a.CrLocale == item.language)?.CrLocale ?? "unknown"
                    );

                LanguageItem? audioLang =
                    item.language != null
                        ? foundLanguage
                        : (language ?? foundLanguage);

                var pItem = new AudioPlaylist{
                    bandwidth = playlist.attributes.BANDWIDTH,
                    audioSamplingRate = ObjectUtilities.GetMemberValue(playlist.attributes, "AUDIOSAMPLINGRATE") ?? 0,
                    language = audioLang,
                    @default = item.@default,
                    segments = segmentsFromSidx ?? ConvertSegments(segments)
                };

                pItem.pssh = ExtractWidevinePssh(playlist);

                ret.Data[host].audio?.Add(pItem);
            }
        }

        foreach (var playlist in parsed.playlists){
            var uri = new Uri(playlist.resolvedUri);
            var host = uri.Host;

            EnsureHostEntryExists(ret, host);

            List<dynamic> segments = playlist.segments;
            List<Segment>? segmentsFromSidx = null;

            if (ObjectUtilities.GetMemberValue(playlist, "sidx") != null){
                if (segments == null || segments.Count == 0){
                    var sidxRange = playlist.sidx.ByteRange;

                    var sidxBytes = await DownloadSidxAsync(
                        HttpClientReq.Instance.GetHttpClient(),
                        playlist.sidx.uri,
                        sidxRange.offset,
                        sidxRange.offset + sidxRange.length - 1);

                    var sidx = ParseSidx(sidxBytes, sidxRange.offset);

                    var byteRange = new ByteRange(){
                        Length = playlist.sidx.map.ByteRange.length,
                        Offset = playlist.sidx.map.ByteRange.offset,
                    };

                    segmentsFromSidx = BuildSegmentsFromSidx(
                        sidx,
                        playlist.resolvedUri,
                        byteRange);
                }
            }

            dynamic resolution =
                ObjectUtilities.GetMemberValue(playlist.attributes, "RESOLUTION") ?? new Quality();

            var pItem = new VideoPlaylist{
                bandwidth = playlist.attributes.BANDWIDTH,
                codecs = ObjectUtilities.GetMemberValue(playlist.attributes, "CODECS") ?? "",
                quality = new Quality{
                    height = resolution.height,
                    width = resolution.width
                },
                segments = segmentsFromSidx ?? ConvertSegments(segments)
            };

            pItem.pssh = ExtractWidevinePssh(playlist);

            ret.Data[host].video?.Add(pItem);
        }

        return ret;
    }

    private static List<Segment> ConvertSegments(List<dynamic>? segments){
        return segments?.Select(segment => new Segment{
            duration = segment.duration,
            timeline = segment.timeline,
            number = segment.number,
            presentationTime = segment.presentationTime,
            uri = segment.resolvedUri,
            byteRange = ObjectUtilities.GetMemberValue(segment, "byterange"),

            map = new Map{
                uri = segment.map.resolvedUri,
                byteRange = ObjectUtilities.GetMemberValue(segment.map, "byterange")
            }
        }).ToList() ?? [];
    }

    private static string? ExtractWidevinePssh(dynamic playlist){
        var dict =
            ObjectUtilities.GetMemberValue(playlist, "contentProtection")
                as IDictionary<string, dynamic>;

        if (dict == null)
            return null;

        if (!dict.TryGetValue("com.widevine.alpha", out var widevine))
            return null;

        if (widevine.pssh == null)
            return null;

        return Convert.ToBase64String(widevine.pssh);
    }

    private static void EnsureHostEntryExists(MPDParsed ret, string host){
        if (!ret.Data.ContainsKey(host)){
            ret.Data[host] = new ServerData{
                audio = new List<AudioPlaylist>(),
                video = new List<VideoPlaylist>()
            };
        }
    }

    public static List<Segment> BuildSegmentsFromSidx(
        SidxInfo sidx,
        string uri,
        ByteRange mapRange){
        var segments = new List<Segment>();

        foreach (var r in sidx.References){
            segments.Add(new Segment{
                uri = uri,
                duration = (double)r.Duration / sidx.Timescale,
                presentationTime = r.PresentationTime,
                timeline = 0,

                map = new Map{
                    uri = uri,
                    byteRange = mapRange
                },

                byteRange = new ByteRange{
                    Offset = r.Offset,
                    Length = r.Size
                }
            });
        }

        return segments;
    }

    static uint ReadUInt32BE(BinaryReader reader){
        var b = reader.ReadBytes(4);
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }

    static ushort ReadUInt16BE(BinaryReader reader){
        var b = reader.ReadBytes(2);
        return (ushort)(b[0] << 8 | b[1]);
    }

    static ulong ReadUInt64BE(BinaryReader reader){
        var b = reader.ReadBytes(8);

        return
            ((ulong)b[0] << 56) |
            ((ulong)b[1] << 48) |
            ((ulong)b[2] << 40) |
            ((ulong)b[3] << 32) |
            ((ulong)b[4] << 24) |
            ((ulong)b[5] << 16) |
            ((ulong)b[6] << 8) |
            b[7];
    }

    public static SidxInfo ParseSidx(byte[] data, long sidxOffset){
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        uint size = ReadUInt32BE(reader);
        string type = new string(reader.ReadChars(4));

        if (type != "sidx")
            throw new Exception("Not a SIDX box");

        byte version = reader.ReadByte();
        reader.ReadBytes(3); // flags

        uint referenceId = ReadUInt32BE(reader);
        uint timescale = ReadUInt32BE(reader);

        ulong earliestPresentationTime;
        ulong firstOffset;

        if (version == 0){
            earliestPresentationTime = ReadUInt32BE(reader);
            firstOffset = ReadUInt32BE(reader);
        } else{
            earliestPresentationTime = ReadUInt64BE(reader);
            firstOffset = ReadUInt64BE(reader);
        }

        reader.ReadUInt16();
        ushort referenceCount = ReadUInt16BE(reader);

        long sidxEnd = sidxOffset + data.Length;
        long offset = sidxEnd + (long)firstOffset;

        var references = new List<SidxReference>();

        for (int i = 0; i < referenceCount; i++){
            uint refInfo = ReadUInt32BE(reader);

            bool referenceType = (refInfo & 0x80000000) != 0;
            uint referenceSize = refInfo & 0x7FFFFFFF;

            uint subsegmentDuration = ReadUInt32BE(reader);
            uint sap = ReadUInt32BE(reader);

            references.Add(new SidxReference{
                Size = referenceSize,
                Duration = subsegmentDuration,
                Offset = offset,
                PresentationTime = (long)earliestPresentationTime
            });

            offset += referenceSize;
            earliestPresentationTime += subsegmentDuration;
        }

        return new SidxInfo{
            Timescale = timescale,
            References = references
        };
    }

    public static async Task<byte[]> DownloadSidxAsync(
        HttpClient httpClient,
        string url,
        long start,
        long end){
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    public class SidxInfo{
        public uint Timescale{ get; set; }
        public List<SidxReference> References{ get; set; }
    }

    public class SidxReference{
        public long Size{ get; set; }
        public long Duration{ get; set; }
        public long Offset{ get; set; }
        public long PresentationTime{ get; set; }
    }
}