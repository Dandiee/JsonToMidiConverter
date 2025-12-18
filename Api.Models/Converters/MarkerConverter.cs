using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Models.Converters;

public class MarkerConverter : JsonConverter<Marker>
{
    public override Marker Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.StartObject => JsonSerializer.Deserialize<Marker>(ref reader, options),
            JsonTokenType.String => new Marker { Text = reader.GetString() },

            _ => throw new NotSupportedException($"Unexpected token type for Marker: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, Marker value, JsonSerializerOptions options)
        => throw new NotImplementedException();
}