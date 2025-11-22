namespace JsonToMidiConverter.Models.Song;

public sealed class Tempó
{
    public int measure { get; set; }
    public double position { get; set; }
    public int bpm { get; set; }
    public int type { get; set; }
    public bool visible { get; set; }
}