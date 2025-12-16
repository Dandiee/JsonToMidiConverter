using System.Text.Json;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Models.Song.Enums;

namespace JsonToMidiConverter.Models.Song.JsonConverters;

public class DirectionConverter : JsonConverter<Direction>
{
    public override Direction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            return reader.GetBoolean() ? Direction.Down : Direction.None;
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();

            if (Enum.TryParse<Direction>(stringValue, ignoreCase: true, out var result))
            {
                return result;
            }
        }

        return Direction.None;
    }

    public override void Write(Utf8JsonWriter writer, Direction value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}