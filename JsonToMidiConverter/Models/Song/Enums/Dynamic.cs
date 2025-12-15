using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Dynamic : byte
{
    Unset,

    Crescendo,
    Decrescendo
}