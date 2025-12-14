using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("V{Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Voice
{
    public List<Beat> Beats { get; set; } = [];

    public bool Rest { get; set; }
    public bool HasSameRhythm { get; set; }

    public Voice Clone() => new()
    {
        Rest = Rest,
        Beats = Beats.Select(b => b.Clone()).ToList(),
        HasSameRhythm = HasSameRhythm
    };
}