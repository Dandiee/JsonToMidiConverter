namespace JsonToMidiConverter.Models.Song;

public sealed class Tempó : MeasureTempo
{
    public ushort Measure { get; set; }
    public float Position { get; set; }
    public bool Visible { get; set; }
    public bool Linear { get; set; }
    public string? Text { get; set; }
    public bool Dotted { get; set; }
    
}

public class MeasureTempo : ISerializable
{
    public int Type { get; set; }
    public int Bpm { get; set; }
    public int Progressive { get; set; }

    public MeasureTempo Clone() => new()
    {
        Type = Type,
        Bpm = Bpm
    };
}