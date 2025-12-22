using System.Text.Json;
using System.Text.Json.Serialization;
using Dani.Data.Factories;
using Dani.Data.Models.Parts;

namespace Dani.Data.Json.Converters;

public class BeatConverter : JsonConverter<Beat>
{
    public override Beat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var rawBeat = JsonSerializer.Deserialize<RawBeat>(ref reader, options);
        return BeatFactory.FromRaw(rawBeat);
    }

    public override void Write(Utf8JsonWriter writer, Beat value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}