using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Direction : byte
{
    None,

    Up,
    Down
}

public enum StrokeTechnique : byte
{
    None = 0,     // Standard Strum
    Arpeggio = 1, // Rolled Chord
    Rasgueado = 2 // Flamenco Fan
}