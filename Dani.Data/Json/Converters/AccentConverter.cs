using System.Text.Json;
using System.Text.Json.Serialization;
using Dani.Data.Models.Enums;

namespace Dani.Data.Json.Converters;

public class AccentConverter : JsonConverter<Accent>
{
    public override Accent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => FromFloat(reader.GetSingle()),
            JsonTokenType.True => reader.GetBoolean() ? Accent.Normal : Accent.None,
            JsonTokenType.False => reader.GetBoolean() ? Accent.Normal : Accent.None,

            _ => throw new NotSupportedException($"Unexpected token type for PickStroke: {reader.TokenType}")
        };
    public override void Write(Utf8JsonWriter writer, Accent value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);

    private static Accent FromFloat(float value) =>
        value switch
        {
            0f => Accent.None,
            1f => Accent.Normal,
            2f => Accent.Heavy,

            _ => throw new NotSupportedException($"Unsupported float value for Accent: {value}")
        };
}