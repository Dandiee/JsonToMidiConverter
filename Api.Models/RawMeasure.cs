using Api.Models.Enums;

namespace Api.Models;

public sealed class RawMeasure
{
    public List<RawVoice> Voices { get; set; } = [];
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