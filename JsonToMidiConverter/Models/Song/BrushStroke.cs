using JsonToMidiConverter.Models.Song.Enums;

namespace JsonToMidiConverter.Models.Song;

public class BrushStroke
{
    public Direction Direction { get; set; }
    public double Duration { get; set; }
    public double Shift { get; set; }

    public BrushStroke Clone() => new()
    {
        Direction = Direction,
        Duration = Duration,
        Shift = Shift
    };
}