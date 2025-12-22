using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dani.Data.Json.Converters;

public class VibratoConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.True => reader.GetBoolean(),
            JsonTokenType.False => reader.GetBoolean(),
            JsonTokenType.Number => ConvertToBool(reader.GetDouble()),

            _ => throw new NotSupportedException($"Unexpected token type for PickStroke: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);

    private static bool ConvertToBool(double number)
    {
        if (number == 0 || number == 1) return number == 1;

        throw new NotSupportedException($"Unexpected value for Vibrato: {number}");
    }
}