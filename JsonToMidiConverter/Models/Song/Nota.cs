using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("N{Index} B{Beat.Index} M{Measure.Index} P{Part.Index} STR{StringNumber}/FRT{Fret} NN{NoteNumber}")]
public sealed partial class Nota
{
    public int Fret { get; set; }
    [JsonPropertyName("string")]
    public double StringNumber { get; set; }
    [JsonPropertyName("slide")]
    public string? SlideString { get; set; }
    public bool Vibrato { get; set; }
    public bool Hp { get; set; }
    public bool Tie { get; set; }
    public bool Rest { get; set; }
    public bool Staccato { get; set; }
    public double Accentuated { get; set; }
    public bool Ghost { get; set; }
    public string? Harmonic { get; set; }
    public double HarmonicFret { get; set; }
    public Bend? Bend { get; set; }
    public bool Dead { get; set; }
    public List<long> Tremolo { get; set; } = [];
    public bool WideVibrato { get; set; }

    public Nota Clone() => new()
    {
        Fret = Fret,
        StringNumber = StringNumber,
        SlideString = SlideString,
        Vibrato = Vibrato,
        Hp = Hp,
        Tie = Tie,
        Rest = Rest,
        Staccato = Staccato,
        Accentuated = Accentuated,
        Ghost = Ghost,
        Harmonic = Harmonic,
        HarmonicFret = HarmonicFret,
        Bend = Bend?.Clone(),
        Dead = Dead,
        Tremolo = Tremolo.Select(e => e).ToList(),
        WideVibrato = WideVibrato,
    };
}