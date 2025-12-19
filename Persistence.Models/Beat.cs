using System.Runtime.Serialization;
using Api.Models;
using Api.Models.Enums;
using Persistence.Models.Enums;

namespace Persistence.Models;


public sealed class Beat : Poolable<Beat>
{
    public Beat() { }

    [IgnoreDataMember] public List<Note> Notes { get; set; } = []; 
    public MusicalFraction Duration { get; set; } = MusicalFraction.Zero;
    public Bend? Tremolo { get; set; }
    public ChordStroke? Stroke { get; set; }

    public Harmonic Harmonic { get; set; }
    public Vibrato Vibrato { get; set; }
    public Technique Technique { get; set; }
    public Spanner BeamSpan { get; set; }
    public Direction PickDirection { get; set; }
    public Dot Dots { get; set; }
    public GradualVelocity GradualVelocity { get; set; }
    public Octave Octave { get; set; }
    public Spanner TupletSpan { get; set; }
    public byte TupletDenominator { get; set; }

    public bool PalmMute { get; set; }
    public bool LetRing { get; set; }
    public bool Rest { get; set; }
}