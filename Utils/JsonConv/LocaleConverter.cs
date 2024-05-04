using System;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace CRD.Utils.JsonConv;

public class LocaleConverter : JsonConverter{
    public override bool CanConvert(Type objectType){
        return objectType == typeof(Locale);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer){
        if (reader.TokenType == JsonToken.Null)
            return Locale.Unknown;

        var value = reader.Value?.ToString();

        foreach (Locale locale in Enum.GetValues(typeof(Locale))){
            FieldInfo fi = typeof(Locale).GetField(locale.ToString());
            EnumMemberAttribute[] attributes = (EnumMemberAttribute[])fi.GetCustomAttributes(typeof(EnumMemberAttribute), false);
            if (attributes.Length > 0 && attributes[0].Value == value)
                return locale;
        }

        return Locale.Unknown; // Default to defaulT if no match is found
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer){
        FieldInfo? fi = value?.GetType().GetField(value.ToString() ?? string.Empty);
        EnumMemberAttribute[] attributes = (EnumMemberAttribute[])fi.GetCustomAttributes(typeof(EnumMemberAttribute), false);

        if (attributes.Length > 0 && !string.IsNullOrEmpty(attributes[0].Value))
            writer.WriteValue(attributes[0].Value);
        else
            writer.WriteValue(value?.ToString());
    }
}