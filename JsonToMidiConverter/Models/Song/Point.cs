namespace JsonToMidiConverter.Models.Song;

public class BasePoint : ISerializable
{
    public byte Position { get; set; } // 0 - 60
    public short Tone { get; set; } // -800 - 600

    public virtual BasePoint Clone() => new()
    {
        Position = Position,
        Tone = Tone,
    };
}

public sealed class Point : BasePoint
{
    public byte Vibrato { get; set; }

    public override Point Clone() => new()
    {
        Position = Position,
        Tone = Tone,
        Vibrato = Vibrato
    };
}