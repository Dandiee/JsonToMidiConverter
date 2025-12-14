using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Models.Song.Enums;
using JsonToMidiConverter.Models.Song.JsonConverters;

namespace JsonToMidiConverter.Models.Song;



[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed partial class Measure
{
    public List<Voice> Voices { get; set; } = [];
    
    [JsonPropertyName("signature")]

    [JsonConverter(typeof(MusicalFractionConverter))]
    public MusicalFraction? SignatureArray { get; set; }

    [JsonConverter(typeof(MarkerConverter))]
    public Marker? Marker { get; set; }
    public bool Rest { get; set; }
    public bool RepeatStart { get; set; }
    public byte Repeat { get; set; }
    public bool DoubleBarLine { get; set; }
    public List<byte> AlternateEnding { get; set; } = [];

    [JsonConverter(typeof(TripletFeelConverter))]
    public TripletFeel TripletFeel { get; set; }
    public MeasureTempo? Tempo { get; set; }

    public Measure Clone() => new()
    {
        Voices = Voices.Select(v => v.Clone()).ToList(),
        SignatureArray = SignatureArray?.Copy(),
        Marker = Marker?.Clone(),
        Rest = Rest,
        RepeatStart = RepeatStart,
        Repeat = Repeat,
        DoubleBarLine = DoubleBarLine,
        TripletFeel = TripletFeel,
        Tempo = Tempo?.Clone(),

        Signature = Signature,
        OriginalIndex = OriginalIndex,
    };
}