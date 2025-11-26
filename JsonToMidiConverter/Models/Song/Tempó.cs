namespace JsonToMidiConverter.Models.Song;

public sealed class Tempó
{
    public int Measure { get; set; }
    public double Position { get; set; }
    public int Bpm { get; set; }
    public int Type { get; set; }
    public bool Visible { get; set; }
    public bool Linear { get; set; }
}