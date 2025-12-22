using System.Text.Json;
using System.Text.Json.Serialization;
using Dani.Data.Models.Parts;
using InternalTremoloBar = Dani.Data.Models.Parts.InternalTremoloBar;

namespace Dani.Data.Json.Converters;

public class TremoloBarConverter : JsonConverter<TremoloBar>
{
    public override TremoloBar Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.StartObject => JsonSerializer.Deserialize<InternalTremoloBar>(ref reader, options).ToModel(),
            JsonTokenType.True => new TremoloBar { LegacyFlag = reader.GetBoolean() },
            JsonTokenType.False => new TremoloBar { LegacyFlag = reader.GetBoolean() },

            _ => throw new NotSupportedException($"Unexpected token type for PickStroke: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, TremoloBar value, JsonSerializerOptions options)
        => throw new NotImplementedException();
}