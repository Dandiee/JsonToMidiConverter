using Api.Models.Enums;

namespace Api.Models;

public sealed partial class RawBeat
{
    public List<Note> Notes { get; set; } = [];

    public float Type { get; set; }

    public DisplayText? Chord { get; set; }
    public TremoloBar? TremoloBar { get; set; }
    public MusicalFraction Duration { get; set; } = MusicalFraction.Zero;
    public MeasureTempo? Tempo { get; set; }
    public BrushStroke? BrushStroke { get; set; }
    public BrushStroke? Arpeggio { get; set; }
    public DisplayText? Text { get; set; }


    public PickStroke PickStroke { get; set; }
    public Velocity Velocity { get; set; }
    public GraceNote GraceNote { get; set; }
    public GradualVelocity GradualVelocity { get; set; }
    public VibratoWithTremoloBar VibratoWithTremoloBar { get; set; }
    public Golpe Golpe { get; set; }
    public Octave OctaveClef { get; set; }


    public byte VibratoBar { get; set; }
    public byte Dots { get; set; }
    public byte DownArpeggio { get; set; }
    public byte Tuplet { get; set; }
    public byte WideVibratoBar { get; set; }
    public byte UpArpeggio { get; set; }
    public byte UpStroke { get; set; }
    public byte DownStroke { get; set; }


    public bool BeamStart { get; set; }
    public bool BeamStop { get; set; }
    public bool Vibrato { get; set; }
    public bool PalmMute { get; set; }
    public bool LetRing { get; set; }
    public bool Rest { get; set; }
    public bool Tapping { get; set; }
    public bool TupletStart { get; set; }
    public bool TupletStop { get; set; }
    public bool Slapping { get; set; }
    public bool Popping { get; set; }
    public bool WideVibrato { get; set; }
    public bool HasRasgueado { get; set; }
    public bool Dotted { get; set; }
    public bool FadeIn { get; set; }
    public bool Harmonic { get; set; }
    public bool SemiHarmonic { get; set; }
    public bool ArtificialHarmonic { get; set; }
    public bool PinchHarmonic { get; set; }
    public bool TapHarmonic { get; set; }
    public bool DoubleDotted { get; set; }
}