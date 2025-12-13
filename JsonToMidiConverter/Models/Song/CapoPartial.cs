namespace JsonToMidiConverter.Models.Song;

public sealed class CapoPartial
{
    public byte Fret { get; set; }
    public byte[] Strings { get; set; } = [];
}