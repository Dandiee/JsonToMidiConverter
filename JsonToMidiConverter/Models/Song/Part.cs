using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("P{Index}")]
public sealed partial class Part
{
    public string Name { get; set; } = string.Empty;
    public double Balance { get; set; }
    public double Volume { get; set; }
    public Measure[] Measures { get; set; } = Array.Empty<Measure>();
    public int Frets { get; set; }
    public int[] Tuning { get; set; } = Array.Empty<int>();
    public int Strings { get; set; }
    public int InstrumentId { get; set; }
    public string Instrument { get; set; } = string.Empty;
    public Newlyric[] NewLyrics { get; set; } = Array.Empty<Newlyric>();
    public int PartId { get; set; }
    public Automations Automations { get; set; } = new();
    public int Version { get; set; }
    public int SongId { get; set; }
    public int RevisionId { get; set; }
}