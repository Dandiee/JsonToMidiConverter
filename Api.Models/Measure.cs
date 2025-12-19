using Api.Generators;
using Api.Models.Enums;
using Api.Models.Serialization;

namespace Api.Models;

[AutoSerialize]
public sealed partial class Measure : Serializable
{
    public int Index { get; set; }
    public List<Voice> Voices { get; set; } = [];
    public MusicalFraction  Signature { get; set; } = MusicalFraction.Zero;
    public List<byte> AlternateEnding { get; set; } = [];

    public DisplayText? Marker { get; set; }
    public MeasureTempo? Tempo { get; set; }

    public TripletFeel TripletFeel { get; set; }

    public byte Repeat { get; set; }

    public bool Rest { get; set; }
    public bool RepeatStart { get; set; }
    public bool DoubleBarLine { get; set; }

}