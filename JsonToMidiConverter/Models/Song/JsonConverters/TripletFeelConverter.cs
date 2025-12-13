using JsonToMidiConverter.Models.Song.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.JsonConverters;

public class TripletFeelConverter : JsonConverter<TripletFeel?>
{
    public static readonly IReadOnlyDictionary<string, TripletFeel> Mapping = new Dictionary<string, TripletFeel>(StringComparer.OrdinalIgnoreCase)
    {
        ["off"] = TripletFeel.Off,

        ["8th"] = TripletFeel.Eights,
        ["16th"] = TripletFeel.Sixteen,

        ["dotted8th"] = TripletFeel.DottedEight,
        ["dotted16th"] = TripletFeel.DottedSixteens,

        ["scottish8th"] = TripletFeel.ScottishEight,
        ["scottish16th"] = TripletFeel.ScottishSixteens,
    };

    public override TripletFeel? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            return Mapping[stringValue];
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, TripletFeel? value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}