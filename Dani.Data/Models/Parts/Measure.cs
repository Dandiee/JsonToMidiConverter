using Dani.Data.Generators;
using Dani.Data.Models.Enums;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class Measure : Serializable
{
    public int Index { get; set; } // Never use this, it's a bait, it's just that Songster doesn't have a single developer

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