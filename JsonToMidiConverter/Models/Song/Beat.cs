using JsonToMidiConverter.Models.Song.Enums;
using JsonToMidiConverter.Models.Song.JsonConverters;
using Melanchall.DryWetMidi.MusicTheory;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Octave = JsonToMidiConverter.Models.Song.Enums.Octave;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
public partial class Beat : ISerializable
{
    [JsonIgnore] public List<Nota> Notes { get; set; } = [];

    public double Type { get; set; }

    [JsonPropertyName("calculatedTremolo")]
    public Bend? Tremolo { get; set; }

    [JsonPropertyName("calculatedVibrato")]
    public Vibrato Vibrato { get; set; } = Vibrato.None;
    [JsonConverter(typeof(MarkerConverter))] public Marker? Chord { get; set; }

    [JsonPropertyName("duration"), JsonConverter(typeof(MusicalFractionConverter))] 
    public MusicalFraction MusicalFraction { get; set; }

    public MeasureTempo? Tempo { get; set; }

    [JsonPropertyName("pickStroke"), JsonConverter(typeof(DirectionConverter))]
    public Direction PickDirection { get; set; } = Direction.None;
    public ChordStroke? Stroke { get; set; }
    public Text? Text { get; set; }
    public Velocity Velocity { get; set; }
    public GraceNote GraceNote { get; set; }
    public Dynamic GradualVelocity { get; set; }
    public bool PalmMute { get; set; }
    public bool LetRing { get; set; }
    public bool Rest { get; set; }
    public double Dots { get; set; }
    

    [JsonPropertyName("calculatedHarmonic")] 
    public Harmonic Harmonic { get; set; } = Harmonic.Unset;
    public Technique Technique { get; set; } = Technique.None;
    public Spanner BeamSpan { get; set; }
    public Spanner TupletSpan { get; set; } = Spanner.None;
    public double TupletDenominator { get; set; }
    public Golpe Golpe { get; set; } = Golpe.None;
    public Octave Octave { get; set; } = Octave.None;




    [JsonInclude, JsonPropertyName("octaveClef")]
    private string LegacyOctaveClef
    {
        set => Octave = value switch
        {
            "8va" => Octave.Higher,
            "8vb" => Octave.Lower,
            _ => Octave.None
        };
    }

    [JsonInclude, JsonPropertyName("brushStroke")]
    private BrushStroke? LegacyBrushStroke
    {
        set
        {
            if (value == null) return;

            if (value.Direction != Direction.None)
            {
                PickDirection = value.Direction;
            }

            var s = EnsureStroke();
            s.Duration = (double)value.Duration;
            s.StartTimeOffset = (double)value.Shift;
        }
    }

    [JsonInclude, JsonPropertyName("arpeggio")]
    private BrushStroke? LegacyArpeggio
    {
        set
        {
            if (value == null) return;

            if (value.Direction != Direction.None)
            {
                PickDirection = value.Direction;
            }

            var s = EnsureStroke();
            s.Technique = StrokeTechnique.Arpeggio;
            s.Duration = (double)value.Duration;
            s.StartTimeOffset = (double)value.Shift;
        }
    }

    [JsonInclude, JsonPropertyName("hasRasgueado")]
    private bool LegacyHasRasgueado { set { if (value) EnsureStroke().Technique = StrokeTechnique.Rasgueado; } }

    [JsonInclude, JsonPropertyName("upStroke")]
    private double LegacyUpStroke { set { if (value > 0) ConfigureLegacy(Direction.Up, StrokeTechnique.None, value); } }

    [JsonInclude, JsonPropertyName("downStroke")]
    private double LegacyDownStroke { set { if (value > 0) ConfigureLegacy(Direction.Down, StrokeTechnique.None, value); } }

    [JsonInclude, JsonPropertyName("upArpeggio")]
    private double LegacyUpArpeggio { set { if (value > 0) ConfigureLegacy(Direction.Up, StrokeTechnique.Arpeggio, value); } }

    [JsonInclude, JsonPropertyName("downArpeggio")]
    private double LegacyDownArpeggio { set { if (value > 0) ConfigureLegacy(Direction.Down, StrokeTechnique.Arpeggio, value); } }


    private ChordStroke EnsureStroke()
    {
        if (Stroke == null) Stroke = new ChordStroke();
        return Stroke;
    }

    private void ConfigureLegacy(Direction dir, StrokeTechnique tech, double duration)
    {
        PickDirection = dir;
        var s = EnsureStroke();
        s.Technique = tech;
        s.Duration = duration;
    }

    // Inputs for Vibrato and Tremolo
    [JsonInclude, JsonPropertyName("tremoloBar"), JsonConverter(typeof(TremoloBarConverter))]
    private Bend? LegacyTremoloBarObject { set => Tremolo = value; }

    [JsonInclude, JsonPropertyName("vibratoBar")]
    private double LegacyVibratoBar { set { if (value > 0) EnsureWhammy().Style = TremoloStyle.Slight; } }

    [JsonInclude, JsonPropertyName("wideVibratoBar")]
    private double LegacyWideVibratoBar { set { if (value > 0) EnsureWhammy().Style = TremoloStyle.Wide; } }

    [JsonInclude, JsonPropertyName("vibratoWithTremoloBar")]
    private VibratoWithTremoloBar LegacyVibratoWithTremoloBar
    {
        set
        {
            if (value == VibratoWithTremoloBar.Slight)
                EnsureWhammy().Style = TremoloStyle.Slight;
            else if (value == VibratoWithTremoloBar.Wide)
                EnsureWhammy().Style = TremoloStyle.Wide;
        }
    }

    [JsonInclude, JsonPropertyName("vibrato")]
    private bool LegacyVibrato { set { if (value) Vibrato = Vibrato.FingerStandard; } }

    [JsonInclude, JsonPropertyName("wideVibrato")]
    private bool LegacyWideVibrato { set { if (value) Vibrato = Vibrato.FingerWide; } }

    private Bend EnsureWhammy()
    {
        if (Tremolo == null) Tremolo = new Bend();
        return Tremolo;
    }


    [JsonInclude, JsonPropertyName("dotted")]
    private bool LegacyDotted { set { if (value) Dots = 1; } }

    [JsonInclude, JsonPropertyName("doubleDotted")]
    private bool LegacyDoubleDotted { set { if (value) Dots = 2; } }

    [JsonInclude, JsonPropertyName("fadeIn")]
    private bool LegacyFadeIn { set { if (value) GradualVelocity = Dynamic.Crescendo; } }

    [JsonInclude, JsonPropertyName("harmonic")]
    private bool LegacyNaturalHarmonic { set { if (value) Harmonic = Harmonic.Natural; } }

    [JsonInclude, JsonPropertyName("artificialHarmonic")]
    private bool LegacyArtificial { set { if (value) Harmonic = Harmonic.Artificial; } }

    [JsonInclude, JsonPropertyName("pinchHarmonic")]
    private bool LegacyPinch { set { if (value) Harmonic = Harmonic.Pinch; } }

    [JsonInclude, JsonPropertyName("semiHarmonic")]
    private bool LegacySemi { set { if (value) Harmonic = Harmonic.Semi; } }

    [JsonInclude, JsonPropertyName("tapHarmonic")]
    private bool LegacyTapHarmonic { set { if (value) Harmonic = Harmonic.Tapped; } }

    [JsonInclude, JsonPropertyName("slapping")]
    private bool LegacySlapping { set { if (value) Technique = Technique.Slap; } }

    [JsonInclude, JsonPropertyName("popping")]
    private bool LegacyPopping { set { if (value) Technique = Technique.Pop; } }

    [JsonInclude, JsonPropertyName("tapping")]
    private bool LegacyTapping { set { if (value) Technique = Technique.Tap; } }

    [JsonInclude, JsonPropertyName("beamStart")]
    private bool LegacyBeamStart { set { if (value) BeamSpan = Spanner.Start; } }

    [JsonInclude, JsonPropertyName("beamStop")]
    private bool LegacyBeamStop { set { if (value) BeamSpan = Spanner.Stop; } }

    [JsonInclude, JsonPropertyName("tuplet")]
    private double LegacyTuplet { set { if (value > 1) TupletDenominator = value; } }

    [JsonInclude, JsonPropertyName("tupletStart")]
    private bool LegacyTupletStart { set { if (value) TupletSpan = Spanner.Start; } }

    [JsonInclude, JsonPropertyName("tupletStop")]
    private bool LegacyTupletStop { set { if (value) TupletSpan = Spanner.Stop; } }

  

    public Beat Clone() => new()
    {
        Notes = Notes.Select(e => e.Clone()).ToList(),
        Velocity = Velocity,
        Type = Type,
        PalmMute = PalmMute,
        MusicalFraction = MusicalFraction.Copy(),
        Text = Text?.Clone(),
        LetRing = LetRing,
        Dots = Dots,
        Rest = Rest,
        GraceNote = GraceNote,
        Chord = Chord?.Clone(),
        GradualVelocity = GradualVelocity,
        PickDirection = PickDirection,
        Tempo = Tempo?.Clone(),
        Harmonic = Harmonic,
        Vibrato = Vibrato,
        BeamSpan = BeamSpan,
        Technique = Technique,
        TupletDenominator = TupletDenominator,
        TupletSpan = TupletSpan,
        
    };
}