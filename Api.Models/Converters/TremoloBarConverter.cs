using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Models.Converters;

public class TremoloBarConverter : JsonConverter<TremoloBar>
{
    public override TremoloBar Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.StartObject => JsonSerializer.Deserialize<TremoloBar>(ref reader, options)!,
            JsonTokenType.True => new TremoloBar { LegacyFlag = reader.GetBoolean() },
            JsonTokenType.False => new TremoloBar { LegacyFlag = reader.GetBoolean() },

            _ => throw new NotSupportedException($"Unexpected token type for PickStroke: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, TremoloBar value, JsonSerializerOptions options)
        => throw new NotImplementedException();
}