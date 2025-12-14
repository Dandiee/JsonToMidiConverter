using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.JsonConverters;

public class MusicalFractionConverter : JsonConverter<MusicalFraction>
{
    public static readonly MusicalFractionConverter Instance = new();

    public override MusicalFraction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array.");
        }

        // 2. Read the Numerator (First element)
        reader.Read();
        if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected numerator.");
        var numerator = reader.GetByte();

        // 3. Read the Denominator (Second element)
        reader.Read();
        if (reader.TokenType != JsonTokenType.Number && reader.TokenType != JsonTokenType.Null) throw new JsonException("Expected denominator.");
        var denominator = reader.TokenType == JsonTokenType.Null
            ? (byte)0
            : reader.GetDouble();



        // 4. Consume the End of Array ']'
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("Expected end of array.");
        }

        return new MusicalFraction(numerator, (byte)Math.Min(255, denominator));
    }

    public override void Write(Utf8JsonWriter writer, MusicalFraction value, JsonSerializerOptions options)
    {
        // Write it back as a compact array: [1, 4]
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Numerator);
        writer.WriteNumberValue(value.Denominator);
        writer.WriteEndArray();
    }
}