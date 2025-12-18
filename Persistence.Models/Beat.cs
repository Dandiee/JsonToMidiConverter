using Api.Models.Enums;
using Persistence.Models.Enums;

namespace Persistence.Models;


public sealed class Beat
{
    public List<Note> Notes { get; init; } = [];
    public MusicalFraction Duration { get; init; }
    public Bend? Tremolo { get; init; }
    public ChordStroke? Stroke { get; init; }

    public Harmonic Harmonic { get; init; }
    public Vibrato Vibrato { get; init; }
    public Technique Technique { get; init; }
    public Spanner BeamSpan { get; init; }
    public Direction PickDirection { get; init; }
    public Dot Dots { get; init; }
    public GradualVelocity GradualVelocity { get; init; }
    public Octave Octave { get; init; }
    public Spanner TupletSpan { get; init; }
    public byte TupletDenominator { get; init; }

    public bool PalmMute { get; init; }
    public bool LetRing { get; init; }
    public bool Rest { get; init; }
}