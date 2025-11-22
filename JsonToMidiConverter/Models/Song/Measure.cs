using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed partial class Measure
{
    public Voice[] voices { get; set; } = Array.Empty<Voice>();
    public int[] signature { get; set; } = Array.Empty<int>();
    public Marker? marker { get; set; }
    public bool rest { get; set; }
}