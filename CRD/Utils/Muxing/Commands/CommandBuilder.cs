using System.Collections.Generic;
using CRD.Utils.Muxing.Structs;

namespace CRD.Utils.Muxing.Commands;

public abstract class CommandBuilder{
    private protected readonly MergerOptions Options;
    private protected readonly List<string> Args = new();

    public CommandBuilder(MergerOptions options){
        Options = options;
    }

    public abstract string Build();

    private protected void Add(string arg){
        Args.Add(arg);
    }
    
    private protected void AddIf(bool condition, string arg){
        if (condition)
            Add(arg);
    }

    private protected void AddInput(string path){
        Add($"\"{Helpers.AddUncPrefixIfNeeded(path)}\"");
    }

    private protected void AddRange(IEnumerable<string> args){
        Args.AddRange(args);
    }
}