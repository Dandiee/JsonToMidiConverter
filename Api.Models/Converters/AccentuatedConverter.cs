using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Models.Converters;

public class AccentuatedConverter : JsonConverter<float>
{
    public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetSingle(),
            JsonTokenType.True => reader.GetBoolean() ? 1 : 0,
            JsonTokenType.False => reader.GetBoolean() ? 1 : 0,

            _ => throw new NotSupportedException($"Unexpected token type for PickStroke: {reader.TokenType}")
        };
    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}