using System.Text.Json;
using System.Text.Json.Serialization;
using Dani.Data.Factories;
using Dani.Data.Models.Parts;

namespace Dani.Data.Json.Converters;

public class NoteConverter : JsonConverter<Note>
{
    public override Note Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var rawNote = JsonSerializer.Deserialize<RawNote>(ref reader, options);
        return NoteFactory.FromRaw(rawNote);
    }

    public override void Write(Utf8JsonWriter writer, Note value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}