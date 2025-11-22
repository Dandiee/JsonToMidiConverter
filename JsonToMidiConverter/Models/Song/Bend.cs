namespace JsonToMidiConverter.Models.Song;

public sealed class Bend
{
    public double Tone { get; set; }
    public Point[] Points { get; set; } = Array.Empty<Point>();
}