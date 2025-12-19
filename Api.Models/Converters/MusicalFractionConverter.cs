using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Models.Converters;

public class MusicalFractionConverter : JsonConverter<MusicalFraction>
{
    public override MusicalFraction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.True => new MusicalFraction(1, 32),
            JsonTokenType.False => throw new Exception("??"),
            JsonTokenType.StartArray => FromArray(ref reader),

            _ => throw new NotSupportedException($"Unexpected token type for Tremolo (List<ushort>): {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, MusicalFraction value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);

    private static MusicalFraction FromArray(ref Utf8JsonReader reader)
    {
        reader.Read();
        var numerator = FromElement(ref reader) ?? 0;
        reader.Read();
        var denominator = FromElement(ref reader) ?? 0;
        reader.Read();
        
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new Exception("Tremolo array must contain exactly two elements.");
        }

        return new MusicalFraction(numerator, denominator);
    }

    private static ushort? FromElement(ref Utf8JsonReader reader)
        => reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetUInt16(),
            JsonTokenType.Null => null,
            _ => throw new Exception("Invalid token type for FromElement.")
        };
}