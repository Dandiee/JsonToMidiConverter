using Api.Models;
using Api.Models.Enums;

namespace Persistence.Models;

public sealed class Measure
{
    public List<Voice> Voices { get; set; } = [];
    public List<ushort> Signature { get; set; } = [];
    public List<byte> AlternateEnding { get; set; } = [];

    public DisplayText? Marker { get; set; }
    public MeasureTempo? Tempo { get; set; }

    public TripletFeel TripletFeel { get; set; }

    public byte Repeat { get; set; }

    public bool Rest { get; set; }
    public bool RepeatStart { get; set; }
    public bool DoubleBarLine { get; set; }
}