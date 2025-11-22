namespace JsonToMidiConverter.Models.Song;

public sealed class Newlyric
{
    public int line { get; set; }
    public int offset { get; set; }
    public string text { get; set; } = string.Empty;
}