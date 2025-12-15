using JsonToMidiConverter.Models.Song.Enums;
using JsonToMidiConverter.Models.Song.JsonConverters;
using SharpCompress.Compressors.Deflate;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
public partial class Beat
{
    [JsonIgnore]
    public List<Nota> Notes { get; set; } = [];

    public float Type { get; set; }

    [JsonIgnore][JsonConverter(typeof(MarkerConverter))] public Marker? Chord { get; set; }

    [JsonPropertyName("duration"), JsonConverter(typeof(MusicalFractionConverter))] public MusicalFraction DurationArray { get; set; }
    [JsonIgnore] public MeasureTempo? Tempo { get; set; }
    public BrushStroke? BrushStroke { get; set; }
    public BrushStroke? Arpeggio { get; set; }
    [JsonIgnore] public Text? Text { get; set; }


    [JsonConverter(typeof(PickStrokeConverter))] public PickStroke PickStroke { get; set; }
    public Velocity Velocity { get; set; }
    public GraceNote GraceNote { get; set; }
    




    // everything dot related
    public bool Dotted { get; set; }
    public bool DoubleDotted { get; set; }
    public byte Dots { get; set; }

    //[JsonInclude, JsonPropertyName("dotted")]
    //private bool LegacyDotted { set { if (value) Dots = 1; } }

    //[JsonInclude, JsonPropertyName("doubleDotted")]
    //private bool LegacyDoubleDotted { set { if (value) Dots = 2; } }




    public Dynamic GradualVelocity { get; set; }

    [JsonPropertyName("fadeIn")]
    private bool LegacyFadeIn { set { if (value) GradualVelocity = Dynamic.Crescendo; } }

    // everything harmonic related

    public bool Harmonic { get; set; }
    public bool SemiHarmonic { get; set; }
    public bool ArtificialHarmonic { get; set; }
    public bool PinchHarmonic { get; set; }
    public bool TapHarmonic { get; set; }
    //[JsonPropertyName("harmonic")]
    //public Harmonic Harmonic { get; set; } = Enums.Harmonic.Unset;
    //[JsonInclude, JsonPropertyName("harmonic")]
    //private bool LegacyNaturalHarmonic { set { if (value) Harmonic = Enums.Harmonic.Natural; } }
    //
    //[JsonInclude, JsonPropertyName("artificialHarmonic")]
    //private bool LegacyArtificial { set { if (value) Harmonic = Enums.Harmonic.Artificial; } }
    //
    //[JsonInclude, JsonPropertyName("pinchHarmonic")]
    //private bool LegacyPinch { set { if (value) Harmonic = Enums.Harmonic.Pinch; } }
    //
    //[JsonInclude, JsonPropertyName("semiHarmonic")]
    //private bool LegacySemi { set { if (value) Harmonic = Enums.Harmonic.Semi; } }
    //
    //[JsonInclude, JsonPropertyName("tapHarmonic")]
    //private bool LegacyTapHarmonic { set { if (value) Harmonic = Enums.Harmonic.Tapped; } }



    // everything technique related
    public bool Tapping { get; set; }
    public bool Slapping { get; set; }
    public bool Popping { get; set; }
    //[JsonIgnore]
    //public Technique Technique { get; set; } = Technique.None;
    //[JsonInclude, JsonPropertyName("slapping")]
    //private bool LegacySlapping { set { if (value) Technique = Technique.Slap; } }

    //[JsonInclude, JsonPropertyName("popping")]
    //private bool LegacyPopping { set { if (value) Technique = Technique.Pop; } }

    //[JsonInclude, JsonPropertyName("tapping")]
    //private bool LegacyTapping { set { if (value) Technique = Technique.Tap; } }



    // Everything vibrato related
    [JsonIgnore][JsonConverter(typeof(TremoloBarConverter))] public Bend? TremoloBar { get; set; }
    public VibratoWithTremoloBar VibratoWithTremoloBar { get; set; }
    public bool Vibrato { get; set; }
    public bool WideVibrato { get; set; }
    public byte WideVibratoBar { get; set; }
    public byte VibratoBar { get; set; }
    // public Vibrato UnifiedVibrato { get; set; } = Enums.Vibrato.None;
    // 
    // [JsonInclude, JsonPropertyName("vibratoBar")]
    // private byte LegacyVibratoBar
    // {
    //     set
    //     {
    //         if (value > 0) UnifiedVibrato |= Enums.Vibrato.BarSlight;
    //     }
    // }
    // 
    // [JsonInclude, JsonPropertyName("wideVibratoBar")]
    // private byte LegacyWideVibratoBar
    // {
    //     set
    //     {
    //         if (value > 0) UnifiedVibrato |= Enums.Vibrato.BarWide;
    //     }
    // }
    // 
    // [JsonInclude, JsonPropertyName("vibrato")]
    // private bool LegacyVibrato
    // {
    //     set
    //     {
    //         if (value) UnifiedVibrato |= Enums.Vibrato.FingerStandard;
    //     }
    // }
    // 
    // [JsonInclude, JsonPropertyName("wideVibrato")]
    // private bool LegacyWideVibrato
    // {
    //     set
    //     {
    //         if (value) UnifiedVibrato |= Enums.Vibrato.FingerWide;
    //     }
    // }
    // 
    // [JsonInclude, JsonPropertyName("vibratoWithTremoloBar")]
    // private VibratoWithTremoloBar LegacyVibratoWithTremoloBar
    // {
    //     set
    //     {
    //         UnifiedVibrato |= value switch
    //         {
    //             VibratoWithTremoloBar.Slight => Enums.Vibrato.BarSlight,
    //             VibratoWithTremoloBar.Wide => Enums.Vibrato.BarWide,
    //             _ => Enums.Vibrato.None
    //         };
    //     }
    // }

    

    public bool PalmMute { get; set; }
    public bool LetRing { get; set; }
    public bool Rest { get; set; }
    public bool HasRasgueado { get; set; }
    




    public bool BeamStart { get; set; }
    public bool BeamStop { get; set; }

    // everything beam realted

    // public Spanner BeamSpan { get; set; }
    // 
    // [JsonInclude, JsonPropertyName("beamStart")] 
    // private bool LegacyBeamStart { set { if (value) BeamSpan = Spanner.Start; } }
    // 
    // [JsonInclude, JsonPropertyName("beamStop")] 
    // private bool LegacyBeamStop { set { if (value) BeamSpan = Spanner.Stop; } }

    // everything tuplet related

    public byte Tuplet { get; set; }
    public bool TupletStart { get; set; }
    public bool TupletStop { get; set; }

    // [JsonIgnore] public Spanner TupletSpan { get; set; } = Spanner.None;
    // [JsonIgnore] public byte TupletDenominator { get; set; }
    // 
    // [JsonInclude, JsonPropertyName("tuplet")]
    // private byte LegacyTuplet { set { if (value > 1) TupletDenominator = value; } }
    // 
    // [JsonInclude, JsonPropertyName("tupletStart")]
    // private bool LegacyTupletStart { set { if (value) TupletSpan = Spanner.Start; } }
    // 
    // [JsonInclude, JsonPropertyName("tupletStop")]
    // private bool LegacyTupletStop { set { if (value) TupletSpan = Spanner.Stop; } }




    // everything brush related
    public byte DownArpeggio { get; set; }
    public byte UpArpeggio { get; set; }
    public byte UpStroke { get; set; }
    public byte DownStroke { get; set; }

    //[JsonIgnore] public BrushType BrushType { get; set; } = BrushType.None;

    //[JsonIgnore] public byte BrushDuration { get; set; }


    //[JsonInclude, JsonPropertyName("upStroke")]
    //private byte LegacyUpStroke { set { if (value > 0) SetBrush(BrushType.StrokeUp, value); } }

    //[JsonInclude, JsonPropertyName("downStroke")]
    //private byte LegacyDownStroke { set { if (value > 0) SetBrush(BrushType.StrokeDown, value); } }

    //[JsonInclude, JsonPropertyName("upArpeggio")]
    //private byte LegacyUpArpeggio { set { if (value > 0) SetBrush(BrushType.ArpeggioUp, value); } }

    //[JsonInclude, JsonPropertyName("downArpeggio")]
    //private byte LegacyDownArpeggio { set { if (value > 0) SetBrush(BrushType.ArpeggioDown, value); } }

    //private void SetBrush(BrushType type, byte duration)
    //{
    //    BrushType = type;
    //    BrushDuration = duration;
    //}







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
        VibratoWithTremoloBar = VibratoWithTremoloBar,
        PickStroke = PickStroke,
        TremoloBar = TremoloBar?.Clone(),
        WideVibrato = WideVibrato,
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
        VibratoBar = VibratoBar,
        WideVibratoBar = WideVibratoBar
    };
}