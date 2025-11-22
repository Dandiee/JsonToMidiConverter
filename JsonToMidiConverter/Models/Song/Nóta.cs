using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("N{Index} B{Beat.Index} M{Measure.Index} P{Part.Index} STR{StringNumber}/FRT{fret} NN{NoteNumber}")]
public sealed partial class Nóta
{
    public int fret { get; set; }
    [JsonPropertyName("string")]
    public double StringNumber { get; set; }
    [JsonPropertyName("slide")]
    public string? slideString { get; set; }
    public bool vibrato { get; set; }
    public bool hp { get; set; }
    public bool tie { get; set; }
    public bool rest { get; set; }
    //public int[] tremolo { get; set; }
    public bool staccato { get; set; }
    public double accentuated { get; set; }
    public bool ghost { get; set; }
    public string? harmonic { get; set; }
    public double harmonicFret { get; set; }
    public Bend? bend { get; set; }
    public bool dead { get; set; }
}