using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.JsonConverters;

public class MarkerConverter : JsonConverter<Marker?>
{
    public override Marker? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return JsonSerializer.Deserialize<Marker>(ref reader, options);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return new Marker
            {
                Text = reader.GetString()
            };
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, Marker? value, JsonSerializerOptions options)
    {
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