using JsonToMidiConverter.Models.Song.Enums;

namespace JsonToMidiConverter.Models.Song;

public class BrushStroke
{
    public PickStroke Direction { get; set; }
    public int Duration { get; set; }
    public double Shift { get; set; }

    public BrushStroke Clone() => new()
    {
        Direction = Direction,
        Duration = Duration,
        Shift = Shift
    };
}