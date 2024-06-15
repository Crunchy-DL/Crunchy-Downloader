using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Xml;
using CRD.Utils.Parser.Utils;
using Newtonsoft.Json;

namespace CRD.Utils.Parser;

public class DashParser{

    public static dynamic Parse(string manifest, dynamic? options = null){
        var parsedManifestInfo = InheritAttributes.InheritAttributesFun(StringToMpdXml(manifest));
        List<dynamic> playlists = ToPlaylistsClass.ToPlaylists(parsedManifestInfo.representationInfo);

        dynamic parsedElement = new{
            dashPlaylist = playlists,
            locations= parsedManifestInfo.locations,
            contentSteering= parsedManifestInfo.contentSteeringInfo,
            sidxMapping= options != null ? ObjectUtilities.GetMemberValue(options,"sidxMapping") : null,
            previousManifest= options != null ? ObjectUtilities.GetMemberValue(options,"previousManifest") : null,
            eventStream= ObjectUtilities.GetMemberValue(parsedManifestInfo,"eventStream")
        };
        
        return ToM3u8Class.ToM3u8(parsedElement);
        // string jsonString = JsonConvert.SerializeObject(M3u8);
    }
    
    private static XmlElement StringToMpdXml(string manifestString){
        if (string.IsNullOrEmpty(manifestString))
        {
            throw new Exception(Errors.DASH_EMPTY_MANIFEST);
        }

        XmlDocument xml = new XmlDocument();
        XmlElement mpd = null;

        try
        {
            xml.LoadXml(manifestString);
            mpd = xml.DocumentElement.Name == "MPD" ? xml.DocumentElement : null;
        }
        catch (XmlException)
        {
            // ie 11 throws on invalid xml
        }

        if (mpd == null || (mpd != null && mpd.GetElementsByTagName("parsererror").Count > 0))
        {
            throw new Exception(Errors.DASH_INVALID_XML);
        }

        return mpd;
    }
    
}