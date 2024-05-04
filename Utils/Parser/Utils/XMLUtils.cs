using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace CRD.Utils.Parser.Utils;

public class XMLUtils{
    public static List<XmlElement> FindChildren(XmlElement element, string name){
        return From(element.ChildNodes).OfType<XmlElement>().Where(child => child.Name == name).ToList();
    }

    public static string GetContent(XmlElement element){
        return element.InnerText.Trim();
    }

    private static List<XmlNode> From(XmlNodeList list){
        if (list.Count == 0){
            return new List<XmlNode>();
        }

        List<XmlNode> result = new List<XmlNode>(list.Count);

        for (int i = 0; i < list.Count; i++){
            result.Add(list[i]);
        }

        return result;
    }
}