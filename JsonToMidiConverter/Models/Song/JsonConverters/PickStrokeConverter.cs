using System.Text.Json;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Models.Song.Enums;

namespace JsonToMidiConverter.Models.Song.JsonConverters;

public class PickStrokeConverter : JsonConverter<PickStroke?>
{
    public override PickStroke? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            return reader.GetBoolean() ? PickStroke.Down : null;
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();

            if (Enum.TryParse<PickStroke>(stringValue, ignoreCase: true, out var result))
            {
                return result;
            }
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, PickStroke? value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}