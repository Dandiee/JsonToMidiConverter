using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

public enum BrushType : byte
{
    None = 0,

    StrokeUp,
    StrokeDown,

    ArpeggioUp,
    ArpeggioDown
}