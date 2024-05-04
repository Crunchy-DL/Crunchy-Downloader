namespace CRD.Utils.Structs;

public class Variable{
    public string Name{ get; set; }
    public object ReplaceWith{ get; set; }
    public string Type{ get; set; }
    public bool Sanitize{ get; set; }

    public Variable(string name, object replaceWith, bool sanitize){
        Name = name;
        ReplaceWith = replaceWith;
        Type = replaceWith.GetType().Name.ToLower();
        Sanitize = sanitize;
    }

    public Variable(){
    }
}