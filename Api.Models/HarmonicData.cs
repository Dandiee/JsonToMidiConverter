namespace Api.Models;

public sealed class HarmonicData
{
    public string Type { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public byte Shift { get; set; }
    public sbyte Fret { get; set; }
}