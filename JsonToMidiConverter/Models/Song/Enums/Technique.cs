using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

public enum Technique : byte
{
    None = 0,
    Slap,
    Pop,
    Tap
}