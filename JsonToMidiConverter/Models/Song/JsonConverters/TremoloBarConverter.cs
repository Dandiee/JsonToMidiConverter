using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.JsonConverters;

public class TremoloBarConverter : JsonConverter<Bend?>
{
    public override Bend? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return JsonSerializer.Deserialize<Bend>(ref reader, options);
        }

        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            bool value = reader.GetBoolean();

            if (value)
            {
                return new Bend { LegacyFlag = reader.GetBoolean() };
            }
            return null;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, Bend? value, JsonSerializerOptions options)
    {
        // When writing back to JSON, we always write the object structure
        // unless you specifically want to write 'true' for simple cases.
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}