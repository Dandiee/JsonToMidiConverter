using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.JsonConverters;

public class VibratoConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            return reader.GetBoolean();
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble() > 0;
        }

        return false;
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}