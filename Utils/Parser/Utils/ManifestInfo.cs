using System.Collections.Generic;

namespace CRD.Utils.Parser.Utils;

public class ManifestInfo{
    public dynamic locations{ get; set; }
    public dynamic contentSteeringInfo{ get; set; }
    public dynamic representationInfo{ get; set; }
    public dynamic eventStream{ get; set; }
}