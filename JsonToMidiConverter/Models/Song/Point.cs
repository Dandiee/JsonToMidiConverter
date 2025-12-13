namespace JsonToMidiConverter.Models.Song;

public sealed class Point
{
    public float Position { get; set; }
    public float Tone { get; set; }
    public byte Vibrato { get; set; }

    public Point Clone() => new()
    {
        Position = Position,
        Tone = Tone,
        Vibrato = Vibrato
    };
}