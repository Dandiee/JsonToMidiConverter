using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Models.Factories;
using Api.Models.Parts;

namespace Api.Models.Converters;

public class BeatConverter : JsonConverter<Parts.Beat>
{
    public override Parts.Beat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var rawBeat = JsonSerializer.Deserialize<RawBeat>(ref reader, options);
        return BeatFactory.FromRaw(rawBeat);
    }

    public override void Write(Utf8JsonWriter writer, Parts.Beat value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}