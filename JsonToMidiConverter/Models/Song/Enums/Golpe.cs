using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Golpe : byte
{
    None,

    Finger,
    Thumb,
}


public enum Octave : byte
{
    None,

    Higher,
    Lower
}