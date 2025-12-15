using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Models.Song.Enums;
using JsonToMidiConverter.Models.Song.JsonConverters;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
public partial class Beat
{
    [JsonIgnore]
    public List<Nota> Notes { get; set; } = [];

    public float Type { get; set; }

    [JsonIgnore] [JsonConverter(typeof(MarkerConverter))] public Marker? Chord { get; set; }
    [JsonIgnore] [JsonConverter(typeof(TremoloBarConverter))] public Bend? TremoloBar { get; set; }
    [JsonPropertyName("duration"), JsonConverter(typeof(MusicalFractionConverter))] public MusicalFraction DurationArray { get; set; }
    [JsonIgnore] public MeasureTempo? Tempo { get; set; }
    public BrushStroke? BrushStroke { get; set; }
    public BrushStroke? Arpeggio { get; set; }
    [JsonIgnore] public Text? Text { get; set; }


    [JsonConverter(typeof(PickStrokeConverter))] public PickStroke PickStroke { get; set; }
    public Velocity Velocity { get; set; }
    public GraceNote GraceNote { get; set; }
    public GradualVelocity GradualVelocity { get; set; }

    
    public byte Dots { get; set; }
    public byte DownArpeggio { get; set; }
    public byte Tuplet { get; set; }
    public byte UpArpeggio { get; set; }
    public byte UpStroke { get; set; }
    public byte DownStroke { get; set; }

    public Vibrato UnifiedVibrato { get; set; } = Vibrato.None;

    [JsonInclude, JsonPropertyName("vibratoBar")]
    private byte LegacyVibratoBar
    {
        set
        {
            if (value > 0) UnifiedVibrato |= Vibrato.BarSlight;
        }
    }

    [JsonInclude, JsonPropertyName("wideVibratoBar")]
    private byte LegacyWideVibratoBar
    {
        set
        {
            if (value > 0) UnifiedVibrato |= Vibrato.BarWide;
        }
    }

    [JsonInclude, JsonPropertyName("vibrato")]
    private bool LegacyVibrato
    {
        set
        {
            if (value) UnifiedVibrato |= Vibrato.FingerStandard;
        }
    }

    [JsonInclude, JsonPropertyName("wideVibrato")]
    private bool LegacyWideVibrato
    {
        set
        {
            if (value) UnifiedVibrato |= Vibrato.FingerWide;
        }
    }

    [JsonInclude, JsonPropertyName("vibratoWithTremoloBar")]
    private VibratoWithTremoloBar LegacyVibratoWithTremoloBar
    {
        set
        {
            UnifiedVibrato |= value switch
            {
                VibratoWithTremoloBar.Slight => Vibrato.BarSlight,
                VibratoWithTremoloBar.Wide => Vibrato.BarWide,
                _ => Vibrato.None
            };
        }
    }

    public bool BeamStart { get; set; }
    public bool BeamStop { get; set; }

    public bool PalmMute { get; set; }
    public bool LetRing { get; set; }
    public bool Rest { get; set; }
    
    public bool TupletStart { get; set; }
    public bool TupletStop { get; set; }
    
    public bool HasRasgueado { get; set; }
    public bool Dotted { get; set; }
    public bool FadeIn { get; set; }

    public bool Slapping { get; set; }
    public bool Popping { get; set; }
    public bool Tapping { get; set; }
    public bool Harmonic { get; set; }
    public bool SemiHarmonic { get; set; }
    public bool ArtificialHarmonic { get; set; }
    public bool PinchHarmonic { get; set; }
    public bool TapHarmonic { get; set; }


    public bool DoubleDotted { get; set; }

    


    public Beat Clone() => new()
    {
        Notes = Notes.Select(e => e.Clone()).ToList(),
        Velocity = Velocity,
        Type = Type,
        PalmMute = PalmMute,
        DurationArray = DurationArray.Copy(),
        BeamStart = BeamStart,
        BeamStop = BeamStop,
        Vibrato = Vibrato,
        Text = Text?.Clone(),
        LetRing = LetRing,
        Dots = Dots,
        Rest = Rest,
        Tapping = Tapping,
        Tuplet = Tuplet,
        TupletStart = TupletStart,
        TupletStop = TupletStop,
        GraceNote = GraceNote,
        UpStroke = UpStroke,
        DownStroke = DownStroke,
        Chord = Chord?.Clone(),
        Slapping = Slapping,
        Popping = Popping,
        GradualVelocity = GradualVelocity,
        PickStroke = PickStroke,
        TremoloBar = TremoloBar?.Clone(),
        BrushStroke = BrushStroke?.Clone(),
        DownArpeggio = DownArpeggio,
        HasRasgueado = HasRasgueado,
        Arpeggio = Arpeggio?.Clone(),
        UpArpeggio = UpArpeggio,
        Tempo = Tempo?.Clone(),
        Dotted = Dotted,
        FadeIn = FadeIn,
        Harmonic = Harmonic,
        SemiHarmonic = SemiHarmonic,
        ArtificialHarmonic = ArtificialHarmonic,
        PinchHarmonic = PinchHarmonic,
        DoubleDotted = DoubleDotted,
        TapHarmonic = TapHarmonic,

        UnifiedVibrato = UnifiedVibrato
    };
}