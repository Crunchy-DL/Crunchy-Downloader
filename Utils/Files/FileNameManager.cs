using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CRD.Utils.Structs;

namespace CRD.Utils;

public class FileNameManager{
    public static List<string> ParseFileName(string input, List<Variable> variables, int numbers, List<string> @override){
        Regex varRegex = new Regex(@"\${[A-Za-z1-9]+}");
        var matches = varRegex.Matches(input).Cast<Match>().Select(m => m.Value).ToList();
        var overriddenVars = ParseOverride(variables, @override);
        if (!matches.Any())
            return new List<string>{
                input
            };
        foreach (var match in matches){
            string varName = match.Substring(2, match.Length - 3); // Removing ${ and }
            var variable = overriddenVars.FirstOrDefault(v => v.Name == varName);

            if (variable == null){
                Console.WriteLine($"[ERROR] Found variable '{match}' in fileName but no values was internally found!");
                continue;
            }

            string replacement = variable.ReplaceWith.ToString();
            if (variable.Type == "int32"){
                int len = replacement.Length;
                replacement = len < numbers ? new string('0', numbers - len) + replacement : replacement;
            } else if (variable.Sanitize){
                replacement = CleanupFilename(replacement);
            }

            input = input.Replace(match, replacement);
        }

        return input.Split(Path.DirectorySeparatorChar).Select(CleanupFilename).ToList();
    }

    public static List<Variable> ParseOverride(List<Variable> variables, List<string>? overrides){
        if (overrides == null){
            return variables;
        }
        foreach (var item in overrides){
            int index = item.IndexOf('=');
            if (index == -1){
                Console.WriteLine($"Error: Invalid override format '{item}'");
                continue;
            }

            string[] parts ={ item.Substring(0, index), item.Substring(index + 1) };
            if (!(parts[1].StartsWith("'") && parts[1].EndsWith("'") && parts[1].Length >= 2)){
                Console.WriteLine($"Error: Invalid value format for '{item}'");
                continue;
            }

            parts[1] = parts[1][1..^1]; // Removing the surrounding single quotes
            int alreadyIndex = variables.FindIndex(a => a.Name == parts[0]);

            if (alreadyIndex > -1){
                if (variables[alreadyIndex].Type == "number"){
                    if (!float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float numberValue)){
                        Console.WriteLine($"Error: Wrong type for '{item}'");
                        continue;
                    }

                    variables[alreadyIndex].ReplaceWith = numberValue;
                } else{
                    variables[alreadyIndex].ReplaceWith = parts[1];
                }
            } else{
                bool isNumber = float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedNumber);
                variables.Add(new Variable{
                    Name = parts[0],
                    ReplaceWith = isNumber ? parsedNumber : (object)parts[1],
                    Type = isNumber ? "number" : "string"
                });
            }
        }

        return variables;
    }

    public static string CleanupFilename(string filename){
        string fixingChar = "_";
        Regex illegalRe = new Regex(@"[\/\?<>\\:\*\|"":]"); // Illegal Characters on most Operating Systems
        Regex controlRe = new Regex(@"[\x00-\x1f\x80-\x9f]"); // Unicode Control codes: C0 and C1
        Regex reservedRe = new Regex(@"^\.\.?$"); // Reserved filenames on Unix-based systems (".", "..")
        Regex windowsReservedRe = new Regex(@"^(con|prn|aux|nul|com[0-9]|lpt[0-9])(\..*)?$", RegexOptions.IgnoreCase);
        /*  Reserved filenames in Windows ("CON", "PRN", "AUX", "NUL", "COM1"-"COM9", "LPT1"-"LPT9")
            case-insensitively and with or without filename extensions. */
        Regex windowsTrailingRe = new Regex(@"[\. ]+$");

        filename = illegalRe.Replace(filename, fixingChar);
        filename = controlRe.Replace(filename, fixingChar);
        filename = reservedRe.Replace(filename, fixingChar);
        filename = windowsReservedRe.Replace(filename, fixingChar);
        filename = windowsTrailingRe.Replace(filename, fixingChar);

        return filename;
    }
}