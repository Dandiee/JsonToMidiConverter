using Api.Models.Parts;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Models.Factories;

namespace Api.Models.Converters;

public class NoteConverter : JsonConverter<Parts.Note>
{
    public override Parts.Note Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var rawNote = JsonSerializer.Deserialize<RawNote>(ref reader, options);
        return NoteFactory.FromRaw(rawNote);
    }

    public override void Write(Utf8JsonWriter writer, Parts.Note value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}