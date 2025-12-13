namespace JsonToMidiConverter.Models.Song;

public sealed class NewLyric
{
    public int Line { get; set; }
    public int Offset { get; set; }
    public string Text { get; set; } = string.Empty;
}