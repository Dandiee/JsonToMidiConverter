using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.JsonConverters;

public class TremoloConverter : JsonConverter<MusicalFraction?>
{
    public override MusicalFraction? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            return reader.GetBoolean() ? new MusicalFraction
            {
                Numerator = 1,
                Denominator = 16
            } : null;
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            return MusicalFractionConverter.Instance.Read(ref reader, typeToConvert, options);
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, MusicalFraction value, JsonSerializerOptions options)
        => MusicalFractionConverter.Instance.Write(writer, value, options);
}