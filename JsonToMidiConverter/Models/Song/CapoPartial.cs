namespace JsonToMidiConverter.Models.Song;

public sealed class CapoPartial
{
    public int Fret { get; set; }
    public int[] Strings { get; set; } = [];
}