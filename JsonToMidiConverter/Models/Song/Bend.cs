namespace JsonToMidiConverter.Models.Song;

public sealed class Bend
{
    public double Tone { get; set; }
    public List<Point> Points { get; set; } = [];

    public Bend Clone() => new()
    {
        Tone = Tone,
        Points = Points.Select(e => e.Clone()).ToList()
    };
}