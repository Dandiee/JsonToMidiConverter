using Api.Models.Enums;
using Persistence.Models.Enums;

namespace Persistence.Models;

public sealed class Note
{
    public float StringNumber { get; set; }
    public sbyte Fret { get; init; }
    public List<Slide> Slides { get; init; } = [];
    public Velocity Velocity { get; init; }
    public MusicalFraction? Tremolo { get; init; }
    public Harmonic Harmonic { get; init; }
    public float HarmonicFret { get; init; }
    public Bend? Bend { get; init; }
    public Accent Accentuated { get; init; }
    public Vibrato Vibrato { get; init; }
    public Legato Legato { get; init; }
    public GraceNote Grace { get; set; }

    public bool Tie { get; set; }
    public bool Rest { get; set; }
    public bool Staccato { get; set; }
    public bool Dead { get; set; }
    public bool Ghost { get; set; }
}