using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Models.Parts;

namespace Api.Models.Converters;

public class DisplayTextConverter : JsonConverter<DisplayText>
{
    public override DisplayText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.StartObject => JsonSerializer.Deserialize<InternalDisplayText>(ref reader, options)?.ToModel(),
            JsonTokenType.String => new DisplayText { Text = reader.GetString() },

            _ => throw new NotSupportedException($"Unexpected token type for Marker: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, DisplayText value, JsonSerializerOptions options)
        => throw new NotImplementedException();
}