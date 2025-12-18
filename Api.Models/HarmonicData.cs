using Api.Models.Enums;

namespace Api.Models;

public sealed class HarmonicData
{
    public HarmonicType Type { get; set; }
    public string Note { get; set; } = string.Empty;
    public byte Shift { get; set; }
    public sbyte Fret { get; set; }
}