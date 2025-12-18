using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Models.Enums;

namespace Api.Models.Converters;

public class PickStrokeConverter : JsonConverter<PickStroke>
{
    public override PickStroke Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => reader.GetBoolean() ? PickStroke.True : PickStroke.False,
            JsonTokenType.False => reader.GetBoolean() ? PickStroke.True : PickStroke.False,
            JsonTokenType.String => Enum.Parse<PickStroke>(reader.GetString()!),

            _ => throw new NotSupportedException($"Unexpected token type for PickStroke: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, PickStroke value, JsonSerializerOptions options)
        => throw new NotImplementedException();
}