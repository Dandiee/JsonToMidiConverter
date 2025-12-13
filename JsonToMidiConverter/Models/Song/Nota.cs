using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Models.Song.Enums;
using JsonToMidiConverter.Models.Song.JsonConverters;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("N{Index} B{Beat.Index} M{Measure.Index} P{Part.Index} STR{StringNumber}/FRT{Fret} NN{NoteNumber}")]
public sealed partial class Nota
{
    public int Fret { get; set; }

    [JsonPropertyName("string")]
    public float StringNumber { get; set; }

    [JsonPropertyName("slide")]
    public RawSlide? RawSlide { get; set; }

    [JsonConverter(typeof(VibratoConverter))]
    public bool Vibrato { get; set; }
    public bool Hp { get; set; }
    public bool Tie { get; set; }
    public bool Rest { get; set; }
    public bool Staccato { get; set; }

    [JsonConverter(typeof(AccentuatedConverter))]
    public float Accentuated { get; set; }
    public bool Ghost { get; set; }
    public Harmonic? Harmonic { get; set; }
    public float HarmonicFret { get; set; }
    public Bend? Bend { get; set; }
    public bool Dead { get; set; }

    [JsonConverter(typeof(TremoloConverter))]
    public MusicalFraction? Tremolo { get; set; }

    [JsonConverter(typeof(VibratoConverter))]
    public bool WideVibrato { get; set; }
    public Velocity? Velocity { get; set; }
    public bool Grace { get; set; }
    public HarmonicData? HarmonicData { get; set; }
    public bool Trill { get; set; }

    public Nota Clone() => new()
    {
        Fret = Fret,
        StringNumber = StringNumber,
        RawSlide = RawSlide,
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
        Tremolo = Tremolo?.Copy(),
        WideVibrato = WideVibrato,
        Velocity = Velocity,
        Grace = Grace,
        HarmonicData = HarmonicData?.Clone(),
        Trill = Trill,
    };
}