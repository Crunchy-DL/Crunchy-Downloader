using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using CRD.Utils.Parser.Segments;
using CRD.Utils.Parser.Utils;

namespace CRD.Utils.Parser;

public class ToPlaylistsClass{
    public static List<dynamic> ToPlaylists(IEnumerable<dynamic> representations){
        return representations.Select(GenerateSegments).ToList();
    }

    public static dynamic GenerateSegments(dynamic input){
        dynamic segmentAttributes = new ExpandoObject();
        Func<dynamic, List<dynamic>, List<dynamic>> segmentsFn = null;

        
        if (ObjectUtilities.GetMemberValue(input.segmentInfo,"template") != null){
            segmentsFn = SegmentTemplate.SegmentsFromTemplate;
            segmentAttributes = ObjectUtilities.MergeExpandoObjects(input.attributes, input.segmentInfo.template);
        } else if (ObjectUtilities.GetMemberValue(input.segmentInfo,"@base") != null){
            //TODO
            Console.WriteLine("UNTESTED PARSING");
            segmentsFn = SegmentBase.SegmentsFromBase;
            segmentAttributes = ObjectUtilities.MergeExpandoObjects(input.attributes, input.segmentInfo.@base);
        } else if (ObjectUtilities.GetMemberValue(input.segmentInfo,"list") != null){
            //TODO
            Console.WriteLine("UNTESTED PARSING");
            segmentsFn = SegmentList.SegmentsFromList;
            segmentAttributes = ObjectUtilities.MergeExpandoObjects(input.attributes, input.segmentInfo.list);
        }

        dynamic segmentsInfo = new ExpandoObject();
        segmentsInfo.attributes = input.attributes;

        if (segmentsFn == null){
            return segmentsInfo;
        }

        List<dynamic> segments = segmentsFn(segmentAttributes, input.segmentInfo.segmentTimeline);

        // Duration processing
        if (ObjectUtilities.GetMemberValue(segmentAttributes,"duration") != null){
            int timescale = ObjectUtilities.GetMemberValue(segmentAttributes,"timescale") ?? 1;
            segmentAttributes.duration = ObjectUtilities.GetMemberValue(segmentAttributes,"duration") / timescale;
        } else if (segments.Any()){
            segmentAttributes.duration = segments.Max(segment => Math.Ceiling(ObjectUtilities.GetMemberValue(segment,"duration")));
        } else{
            segmentAttributes.duration = 0;
        }

        segmentsInfo.attributes = segmentAttributes;
        segmentsInfo.segments = segments;

        // sidx box handling
        if (ObjectUtilities.GetMemberValue(input.segmentInfo,"base") != null && ObjectUtilities.GetMemberValue(segmentAttributes,"indexRange") != null){
            segmentsInfo.sidx = segments.FirstOrDefault();
            segmentsInfo.segments = new List<dynamic>();
        }

        return segmentsInfo;
    }
}