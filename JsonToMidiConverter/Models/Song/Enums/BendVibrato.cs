using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

public enum BendVibrato : byte
{
    None = 0,
    Fast = 1,
    Average = 2,
    Slow = 3
}