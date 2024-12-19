using System;
using System.Collections.Generic;
using System.Dynamic;

namespace CRD.Utils.Parser.Utils;

public class ObjectUtilities{
    public static ExpandoObject MergeExpandoObjects(dynamic target, dynamic source){
        var result = new ExpandoObject();
        var resultDict = result as IDictionary<string, object>;

        // Cast source and target to dictionaries if they are not null
        var targetDict = target as IDictionary<string, object>;
        var sourceDict = source as IDictionary<string, object>;

        // If both are null, return an empty ExpandoObject
        if (targetDict == null && sourceDict == null){
            Console.WriteLine("Nothing Merged; both are empty");
            return result; // result is already a new ExpandoObject
        }

        // Copy targetDict into resultDict
        if (targetDict != null){
            foreach (var kvp in targetDict){
                resultDict[kvp.Key] = kvp.Value; // Add or overwrite key-value pairs
            }
        }

        // Copy sourceDict into resultDict, potentially overwriting values from targetDict
        if (sourceDict != null){
            foreach (var kvp in sourceDict){
                resultDict[kvp.Key] = kvp.Value; // Overwrites if key exists
            }
        }

        return result;
    }

    public static void SetAttributeWithDefault(dynamic ob, string attributeName, string defaultValue){
        var obDict = ob as IDictionary<string, object>;

        if (obDict == null){
            throw new ArgumentException("Provided object must be an ExpandoObject.");
        }

        // Check if the attribute exists and is not null or empty
        if (obDict.TryGetValue(attributeName, out object value) && value != null && !string.IsNullOrEmpty(value.ToString())){
            obDict[attributeName] = value;
        } else{
            obDict[attributeName] = defaultValue;
        }
    }

    public static object GetAttributeWithDefault(dynamic ob, string attributeName, string defaultValue){
        var obDict = ob as IDictionary<string, object>;

        if (obDict == null){
            throw new ArgumentException("Provided object must be an ExpandoObject.");
        }

        // Check if the attribute exists and is not null or empty
        if (obDict.TryGetValue(attributeName, out object value) && value != null && !string.IsNullOrEmpty(value.ToString())){
            return value;
        } else{
            return defaultValue;
        }
    }

    public static void SetFieldFromOrToDefault(dynamic targetObject, string fieldToSet, string fieldToGetValueFrom, object defaultValue){
        var targetDict = targetObject as IDictionary<string, object>;

        if (targetDict == null){
            throw new ArgumentException("Provided targetObject must be an ExpandoObject.");
        }

        // Attempt to get the value from the specified field
        object valueToSet = defaultValue;
        if (targetDict.TryGetValue(fieldToGetValueFrom, out object valueFromField) && valueFromField != null){
            valueToSet = valueFromField;
        }

        // Set the specified field to the retrieved value or the default value
        targetDict[fieldToSet] = valueToSet;
    }

    public static object GetMemberValue(dynamic obj, string memberName){
        // First, check if the object is indeed an ExpandoObject
        if (obj is ExpandoObject expando){
            // Try to get the value from the ExpandoObject
            var dictionary = (IDictionary<string, object>)expando;
            if (dictionary.TryGetValue(memberName, out object value)){
                // Return the found value, which could be null
                return value;
            }
        } else if (obj != null){
            // For non-ExpandoObject dynamics, attempt to access the member directly
            // This part might throw exceptions if the member does not exist
            try{
                return obj.GetType().GetProperty(memberName)?.GetValue(obj, null) ??
                       obj.GetType().GetField(memberName)?.GetValue(obj);
            } catch{
                // Member access failed, handle accordingly (e.g., log the issue)
            }
        }

        // Member doesn't exist or obj is null, return null or a default value
        return null;
    }
}