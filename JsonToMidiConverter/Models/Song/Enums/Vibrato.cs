using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

public enum NoteVibrato : byte
{
    None,

    Slight,
    Wide,
}

[Flags] // Critical attribute!
public enum Vibrato : byte
{
    None = 0,

    FingerStandard = 1 << 0, // 1
    FingerWide = 1 << 1, // 2
}