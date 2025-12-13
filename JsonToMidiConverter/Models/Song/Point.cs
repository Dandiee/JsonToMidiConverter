namespace JsonToMidiConverter.Models.Song;

public sealed class Point
{
    public double Position { get; set; }
    public double Tone { get; set; }
    public int Vibrato { get; set; }

    public Point Clone() => new()
    {
        Position = Position,
        Tone = Tone,
        Vibrato = Vibrato
    };
}