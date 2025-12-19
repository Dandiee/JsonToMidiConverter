namespace Api.Models;

public sealed class RawPart
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Instrument { get; set; } = string.Empty;
    public float Balance { get; set; }
    public float Volume { get; set; }

    public sbyte Frets { get; set; }
    public byte Strings { get; set; }
    public ushort InstrumentId { get; set; }
    public byte PartId { get; set; }
    public int Version { get; set; }
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    public int Capo { get; set; }
    public int Voices { get; set; }

    public List<RawMeasure> Measures { get; set; } = [];
    public CapoPartial? CapoPartial { get; set; }
    public TrackAutomations? TrackAutomations { get; set; }

    public List<sbyte> Tuning { get; set; } = [];
    public List<NewLyric> NewLyrics { get; set; } = [];
    public Automations Automations { get; set; } = new();

    public bool WithLyrics { get; set; }
    public bool TuningFlat { get; set; }
    public bool Anacrusis { get; set; }
    public bool TuningShortDrone { get; set; }
}
