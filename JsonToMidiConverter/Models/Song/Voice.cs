using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("V{Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Voice
{
    public bool Rest { get; set; }
    public List<Beat> Beats { get; set; } = [];
}