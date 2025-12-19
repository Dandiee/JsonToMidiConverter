using Api.Models;
using Api.Models.Enums;
using Persistence.Models.Enums;

namespace Persistence.Models;

public sealed class Note : Poolable<Note>
{
    public float StringNumber { get; set; }
    public sbyte Fret { get; set; }
    public List<Slide> Slides { get; set; } = [];
    public Velocity Velocity { get; set; }
    public MusicalFraction? Tremolo { get; set; }
    public Harmonic Harmonic { get; set; }
    public float HarmonicFret { get; set; }
    public Bend? Bend { get; set; }
    public Accent Accentuated { get; set; }
    public Vibrato Vibrato { get; set; }
    public Legato Legato { get; set; }
    public GraceNote Grace { get; set; }

    public bool Tie { get; set; }
    public bool Rest { get; set; }
    public bool Staccato { get; set; }
    public bool Dead { get; set; }
    public bool Ghost { get; set; }
}