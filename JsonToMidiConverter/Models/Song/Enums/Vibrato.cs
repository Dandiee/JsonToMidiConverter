using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[Flags] // Critical attribute!
public enum Vibrato : byte
{
    None = 0,

    // --- Finger Techniques ---
    FingerStandard = 1 << 0, // 1
    FingerWide = 1 << 1, // 2

    // --- Whammy Bar Techniques ---
    // We map both "Old Numeric" and "New Enum" to these values
    BarSlight = 1 << 2, // 4
    BarWide = 1 << 3  // 8
}