using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed partial class Measure
{
    public List<Voice> Voices { get; set; } = [];
    public List<int> Signature { get; set; } = [];
    public Marker? Marker { get; set; }
    public bool Rest { get; set; }
    public bool RepeatStart { get; set; }
    public int Repeat { get; set; }
    public bool DoubleBarline { get; set; }
    public int[] AlternateEnding { get; set; }
    public string TripletFeel { get; set; }

    public Measure Clone() => new()
    {
        Voices = Voices.Select(v => v.Clone()).ToList(),
        Signature = Signature.Select(e => e).ToList(),
        Marker = Marker?.Clone(),
        Rest = Rest,
        RepeatStart = RepeatStart,
        Repeat = Repeat,
        DoubleBarline = DoubleBarline,
        TripletFeel = TripletFeel
    };
}