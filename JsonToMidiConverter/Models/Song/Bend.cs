namespace JsonToMidiConverter.Models.Song;

public sealed class Bend
{
    public double tone { get; set; }
    public Point[] points { get; set; } = Array.Empty<Point>();
}