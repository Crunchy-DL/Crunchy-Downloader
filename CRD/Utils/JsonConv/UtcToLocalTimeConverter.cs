using System;
using Newtonsoft.Json;

namespace CRD.Utils.JsonConv;

public class UtcToLocalTimeConverter : JsonConverter<DateTime>{
    public override DateTime ReadJson(JsonReader reader, Type objectType, DateTime existingValue, bool hasExistingValue, JsonSerializer serializer){
        try{
            return reader.Value switch{
                null => DateTime.MinValue,
                DateTime dateTime when dateTime.Kind == DateTimeKind.Utc => dateTime.ToLocalTime(),
                DateTime dateTime => dateTime,
                string dateString => TryParseDateTime(dateString),
                _ => throw new JsonSerializationException($"Unexpected token parsing date. Expected DateTime or string, got {reader.Value?.GetType()}.")
            };
        } catch (Exception ex){
            Console.Error.WriteLine("Error deserializing DateTime", ex);
        }
        return DateTime.UnixEpoch;
    }

    private DateTime TryParseDateTime(string dateString){
        if (DateTime.TryParse(dateString, out DateTime parsedDate)){
            return parsedDate.Kind == DateTimeKind.Utc ? parsedDate.ToLocalTime() : parsedDate;
        }
        
        throw new JsonSerializationException($"Invalid date string: {dateString}");
    }

    public override void WriteJson(JsonWriter writer, DateTime value, JsonSerializer serializer){
        writer.WriteValue(value);
    }
}