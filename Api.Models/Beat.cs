using Api.Generators;
using Api.Models.Converters;
using Api.Models.Enums;
using Api.Models.Serialization;
using System.Text.Json.Serialization;

namespace Api.Models;

[AutoSerializeIndex]
[JsonConverter(typeof(BeatConverter))]
public sealed partial class Beat : Serializable
{
    public List<Note> Notes { get; set; } = new(8);
    public Bend? Tremolo { get; set; }
    public ChordStroke? Stroke { get; set; }

    public MusicalFraction Duration { get; set; } = MusicalFraction.Zero;

    // 19 bit
    public Harmonic Harmonic { get; set; }                      // 7 [3 bit]
    public Vibrato Vibrato { get; set; }                        // 3 [2 bit]
    public Technique Technique { get; set; }                    // 4 [2 bit]
    public Spanner BeamSpan { get; set; }                       // 3 [2 bit]
    public Direction PickDirection { get; set; }                // 3 [2 bit]
    public Dot Dots { get; set; }                               // 3 [2 bit]
    public GradualVelocity GradualVelocity { get; set; }        // 3 [2 bit]
    public Octave Octave { get; set; }                          // 3 [2 bit]
    public Spanner TupletSpan { get; set; }                     // 3 [2 bit]
    
    // 3 bit
    public bool PalmMute { get; set; }                          // [1 bit]
    public bool LetRing { get; set; }                           // [1 bit]
    public bool Rest { get; set; }                              // [1 bit]

    // 8 bit
    public byte TupletDenominator { get; set; }                 // [8 bit]
}