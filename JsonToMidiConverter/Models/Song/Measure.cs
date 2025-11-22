using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed partial class Measure
{
    public Voice[] Voices { get; set; } = Array.Empty<Voice>();
    public int[] Signature { get; set; } = Array.Empty<int>();
    public Marker? Marker { get; set; }
    public bool Rest { get; set; }
}