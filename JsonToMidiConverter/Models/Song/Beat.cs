using JsonToMidiConverter.Models.Song.Enums;
using JsonToMidiConverter.Models.Song.JsonConverters;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
public partial class Beat
{
    public List<Nota> Notes { get; set; } = [];

    public float Type { get; set; }

    [JsonIgnore][JsonConverter(typeof(MarkerConverter))] public Marker? Chord { get; set; }

    [JsonPropertyName("duration"), JsonConverter(typeof(MusicalFractionConverter))] public MusicalFraction DurationArray { get; set; }
    [JsonIgnore] public MeasureTempo? Tempo { get; set; }
    public BrushStroke? BrushStroke { get; set; }
    public BrushStroke? Arpeggio { get; set; }
    [JsonIgnore] public Text? Text { get; set; }
    [JsonIgnore][JsonConverter(typeof(TremoloBarConverter))] public Bend? TremoloBar { get; set; }

    // these are untouched
    [JsonConverter(typeof(PickStrokeConverter))] public PickStroke PickStroke { get; set; }
    public Velocity Velocity { get; set; }
    public GraceNote GraceNote { get; set; }
    public Dynamic GradualVelocity { get; set; }
    public bool PalmMute { get; set; }
    public bool LetRing { get; set; }
    public bool Rest { get; set; }
    public bool HasRasgueado { get; set; }
    public byte Dots { get; set; }

    // these are calculated
    [JsonIgnore] public Harmonic Harmonic { get; set; } = Harmonic.Unset;
    [JsonIgnore] public Technique Technique { get; set; } = Technique.None;
    [JsonIgnore] public Vibrato Vibrato { get; set; } = Vibrato.None;
    [JsonIgnore] public Spanner BeamSpan { get; set; }
    [JsonIgnore] public Spanner TupletSpan { get; set; } = Spanner.None;
    [JsonIgnore] public byte TupletDenominator { get; set; }
    [JsonIgnore] public Brush Brush { get; set; } = Brush.None;
    [JsonIgnore] public byte BrushDuration { get; set; }



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
    private byte LegacyTuplet { set { if (value > 1) TupletDenominator = value; } }

    [JsonInclude, JsonPropertyName("tupletStart")]
    private bool LegacyTupletStart { set { if (value) TupletSpan = Spanner.Start; } }

    [JsonInclude, JsonPropertyName("tupletStop")]
    private bool LegacyTupletStop { set { if (value) TupletSpan = Spanner.Stop; } }

    [JsonInclude, JsonPropertyName("upStroke")]
    private byte LegacyUpStroke { set { if (value > 0) SetBrush(Brush.StrokeUp, value); } }

    [JsonInclude, JsonPropertyName("downStroke")]
    private byte LegacyDownStroke { set { if (value > 0) SetBrush(Brush.StrokeDown, value); } }

    [JsonInclude, JsonPropertyName("upArpeggio")]
    private byte LegacyUpArpeggio { set { if (value > 0) SetBrush(Brush.ArpeggioUp, value); } }

    [JsonInclude, JsonPropertyName("downArpeggio")]
    private byte LegacyDownArpeggio { set { if (value > 0) SetBrush(Brush.ArpeggioDown, value); } }

    [JsonInclude, JsonPropertyName("vibratoBar")]
    private byte LegacyVibratoBar { set { if (value > 0) Vibrato |= Vibrato.BarSlight; } }

    [JsonInclude, JsonPropertyName("wideVibratoBar")]
    private byte LegacyWideVibratoBar { set { if (value > 0) Vibrato |= Vibrato.BarWide; } }

    [JsonInclude, JsonPropertyName("vibrato")]
    private bool LegacyVibrato { set { if (value) Vibrato |= Vibrato.FingerStandard; } }

    [JsonInclude, JsonPropertyName("wideVibrato")]
    private bool LegacyWideVibrato { set { if (value) Vibrato |= Vibrato.FingerWide; } }

    [JsonInclude, JsonPropertyName("vibratoWithTremoloBar")]
    private VibratoWithTremoloBar LegacyVibratoWithTremoloBar
    {
        set
        {
            Vibrato |= value switch
            {
                VibratoWithTremoloBar.Slight => Vibrato.BarSlight,
                VibratoWithTremoloBar.Wide => Vibrato.BarWide,
                _ => Vibrato.None
            };
        }
    }


    private void SetBrush(Brush type, byte duration)
    {
        Brush = type;
        BrushDuration = duration;
    }

    public Beat Clone() => new()
    {
        Notes = Notes.Select(e => e.Clone()).ToList(),
        Velocity = Velocity,
        Type = Type,
        PalmMute = PalmMute,
        DurationArray = DurationArray.Copy(),
        Text = Text?.Clone(),
        LetRing = LetRing,
        Dots = Dots,
        Rest = Rest,
        GraceNote = GraceNote,
        Chord = Chord?.Clone(),
        GradualVelocity = GradualVelocity,
        PickStroke = PickStroke,
        TremoloBar = TremoloBar?.Clone(),
        BrushStroke = BrushStroke?.Clone(),
        HasRasgueado = HasRasgueado,
        Arpeggio = Arpeggio?.Clone(),
        Tempo = Tempo?.Clone(),
        Harmonic = Harmonic,
        Vibrato = Vibrato,
        BeamSpan = BeamSpan,
        Brush = Brush,
        BrushDuration = BrushDuration,
        Technique = Technique,
        TupletDenominator = TupletDenominator,
        TupletSpan = TupletSpan,
        
    };
}