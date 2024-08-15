using System;
using Newtonsoft.Json;

namespace CRD.Utils.JsonConv;

public class UtcToLocalTimeConverter : JsonConverter<DateTime>{
    public override DateTime ReadJson(JsonReader reader, Type objectType, DateTime existingValue, bool hasExistingValue, JsonSerializer serializer){
        return reader.Value switch{
            null => DateTime.MinValue,
            DateTime dateTime when dateTime.Kind == DateTimeKind.Utc => dateTime.ToLocalTime(),
            DateTime dateTime => dateTime,
            _ => throw new JsonSerializationException($"Unexpected token parsing date. Expected DateTime, got {reader.Value.GetType()}.")
        };
    }

    public override void WriteJson(JsonWriter writer, DateTime value, JsonSerializer serializer){
        writer.WriteValue(value);
    }
}