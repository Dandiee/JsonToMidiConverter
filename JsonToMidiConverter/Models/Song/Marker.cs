namespace JsonToMidiConverter.Models.Song;

public sealed class Marker : ISerializable
{
    public string Text { get; set; } = string.Empty;
    public int Width { get; set; }

    public Marker Clone() => new()
    {
        Text = Text,
        Width = Width
    };
}