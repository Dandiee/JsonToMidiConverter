namespace JsonToMidiConverter.Models.Song;

public sealed class HarmonicData
{
    public string Type { get; set; }
    public string Note { get; set; }
    public int Shift { get; set; }
    public int? Fret { get; set; }

    public HarmonicData Clone() => new()
    {
        Type = Type,
        Note = Note,
        Shift = Shift,
        Fret = Fret
    };
}