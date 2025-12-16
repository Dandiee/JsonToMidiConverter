using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

public enum Accent : byte
{
    None = 0,

    Normal,
    Heavy,
}