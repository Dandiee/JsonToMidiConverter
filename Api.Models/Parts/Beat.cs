using System.Text.Json.Serialization;
using Api.Generators;
using Api.Models.Converters;
using Api.Models.Enums;
using Api.Models.Serialization;

namespace Api.Models.Parts;

[AutoSerializeIndex]
[JsonConverter(typeof(BeatConverter))]
public sealed partial class Beat : Serializable
{
    public List<Note> Notes { get; set; } = new(8);
    public Bend? Tremolo { get; set; }
    public ChordStroke? Stroke { get; set; }

    public MusicalFraction Duration { get; set; } = MusicalFraction.Zero;

    public Harmonic Harmonic { get; set; }
    public Vibrato Vibrato { get; set; }
    public Technique Technique { get; set; }
    public Spanner BeamSpan { get; set; }
    public Direction PickDirection { get; set; }
    public Dot Dots { get; set; }
    public GradualVelocity GradualVelocity { get; set; }
    public Octave Octave { get; set; }
    public Spanner TupletSpan { get; set; }

    // 3 bit
    public bool PalmMute { get; set; }
    public bool LetRing { get; set; }
    public bool Rest { get; set; }

    // 8 bit
    public byte TupletDenominator { get; set; }
}