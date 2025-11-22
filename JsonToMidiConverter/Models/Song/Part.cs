using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("P{Index}")]
public sealed partial class Part
{
    public string name { get; set; } = string.Empty;
    public double balance { get; set; }
    public double volume { get; set; }
    public Measure[] measures { get; set; } = Array.Empty<Measure>();
    public int frets { get; set; }
    public int[] tuning { get; set; } = Array.Empty<int>();
    public int strings { get; set; }
    public int instrumentId { get; set; }
    public string instrument { get; set; } = string.Empty;
    public Newlyric[] newLyrics { get; set; } = Array.Empty<Newlyric>();
    public int partId { get; set; }
    public Automations automations { get; set; } = new();
    public int version { get; set; }
    public int songId { get; set; }
    public int revisionId { get; set; }
}