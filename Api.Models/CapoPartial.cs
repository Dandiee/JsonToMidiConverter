namespace Api.Models;

public sealed class CapoPartial
{
    public byte Fret { get; set; }
    public List<byte> Strings { get; set; } = [];
}