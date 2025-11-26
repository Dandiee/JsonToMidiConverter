using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed partial class Measure
{
    public Voice[] Voices { get; set; } = [];
    public int[] Signature { get; set; } = [];
    public Marker? Marker { get; set; }
    public bool Rest { get; set; }
}