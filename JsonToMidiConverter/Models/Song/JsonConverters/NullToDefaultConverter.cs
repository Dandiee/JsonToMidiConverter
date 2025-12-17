using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.JsonConverters;

public class NullToDefaultConverter<T> : JsonConverter<T> where T : struct
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 1. Handle Null
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        // 2. Handle normal value (prevent recursion by not passing 'options')
        // We use the default behavior for the primitive type.
        return JsonSerializer.Deserialize<T>(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Write the value normally
        JsonSerializer.Serialize(writer, value);
    }
}