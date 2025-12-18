using JsonToMidiConverter.Models.Song.Enums;

namespace JsonToMidiConverter.Models.Song;

public class BrushStroke
{
    public Direction Direction { get; set; }
    public short Duration { get; set; }
    public float Shift { get; set; } // theres a record with value "28.000001" maybe we can just normalize the data

    public BrushStroke Clone() => new()
    {
        Direction = Direction,
        Duration = Duration,
        Shift = Shift
    };
}