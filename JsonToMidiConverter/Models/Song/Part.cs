using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("P{Index}")]
public sealed partial class Part
{
    public string Name { get; set; } = string.Empty;
    public double Balance { get; set; }
    public double Volume { get; set; }
    public List<Measure> Measures { get; set; } = [];
    public int Frets { get; set; }
    public int[] Tuning { get; set; } = [];
    public int Strings { get; set; }
    public int InstrumentId { get; set; }
    public string Instrument { get; set; } = string.Empty;
    public List<Newlyric> NewLyrics { get; set; } = [];
    public int PartId { get; set; }
    public Automations Automations { get; set; } = new();
    public int Version { get; set; }
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    public bool WithLyrics { get; set; }
    public bool TuningFlat { get; set; }
    public bool Anacrusis { get; set; }
    public int Capo { get; set; }
}