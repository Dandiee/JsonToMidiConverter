using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("P{Index}")]
public sealed partial class Part
{
    public string Name { get; set; } = string.Empty;
    public float Balance { get; set; }
    public float Volume { get; set; }
    public List<Measure> Measures { get; set; } = [];
    public sbyte Frets { get; set; }
    public sbyte[] Tuning { get; set; } = [];
    public byte Strings { get; set; }
    public ushort InstrumentId { get; set; }
    public string Instrument { get; set; } = string.Empty;
    public List<NewLyric> NewLyrics { get; set; } = [];
    public byte PartId { get; set; }
    public Automations Automations { get; set; } = new();
    public int Version { get; set; }
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    public bool WithLyrics { get; set; }
    public bool TuningFlat { get; set; }
    public bool Anacrusis { get; set; }
    public int Capo { get; set; }
    public int Voices { get; set; }
    public bool TuningShortDrone { get; set; }
    public CapoPartial? CapoPartial { get; set; }
    public TrackAutomations? TrackAutomations { get; set; }
}
