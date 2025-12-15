namespace JsonToMidiConverter.Models.Song;

public sealed class CapoPartial
{
    public byte Fret { get; set; }
    public List<byte> Strings { get; set; } = [];
}