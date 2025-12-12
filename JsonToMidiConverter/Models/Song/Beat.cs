using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
public partial class Beat
{
    public List<Nota> Notes { get; set; } = [];
    public string Velocity { get; set; } = string.Empty;
    public int Type { get; set; }
    public bool PalmMute { get; set; }
    [JsonPropertyName("duration")]
    public List<long> DurationArray { get; set; } = [];
    public bool BeamStart { get; set; }
    public bool BeamStop { get; set; }
    public bool Vibrato { get; set; }
    public Text? Text { get; set; }
    public bool LetRing { get; set; }
    public int Dots { get; set; }
    public bool Rest { get; set; }
    public bool Tapping { get; set; }
    public int Tuplet { get; set; }
    public bool TupletStart { get; set; }
    public bool TupletStop { get; set; }
    public string? GraceNote { get; set; }
    public int UpStroke { get; set; }
    public int DownStroke { get; set; }
    public Marker? Chord { get; set; }
    public bool Slapping { get; set; }
    public bool Popping { get; set; }
    public string? GradualVelocity { get; set; }
    public string? VibratoWithTremoloBar { get; set; }
    public string? PickStroke { get; set; }
    public Bend? TremoloBar { get; set; }
    public bool WideVibrato { get; set; }
    public BrushStroke? BrushStroke { get; set; }
    public int DownArpeggio { get; set; }
    public bool HasRasgueado { get; set; }
    public BrushStroke? Arpeggio { get; set; }
    public int UpArpeggio { get; set; }
    public object Tempo { get; set; }
    public bool Dotted { get; set; }
    public bool FadeIn { get; set; }

    public Beat Clone() => new()
    {
        Notes = Notes.Select(e => e.Clone()).ToList(),
        Velocity = Velocity,
        Type = Type,
        PalmMute = PalmMute,
        DurationArray = DurationArray.Select(e => e).ToList(),
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
        Tempo = Tempo,
        Dotted = Dotted,
        FadeIn = FadeIn
    };
}

public class BrushStroke
{
    public string Direction { get; set; }
    public int Duration { get; set; }
    public double Shift { get; set; }

    public BrushStroke Clone() => new()
    {
        Direction = Direction,
        Duration = Duration,
        Shift = Shift
    };
}