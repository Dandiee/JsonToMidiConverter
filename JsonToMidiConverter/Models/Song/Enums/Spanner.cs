using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Spanner : byte
{
    None,

    Start,
    Stop
}