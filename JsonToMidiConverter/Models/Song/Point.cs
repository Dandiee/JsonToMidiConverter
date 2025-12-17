namespace JsonToMidiConverter.Models.Song;

public class BasePoint : ISerializable
{
    public double Position { get; set; }
    public double Tone { get; set; }

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