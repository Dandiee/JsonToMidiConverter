namespace JsonToMidiConverter.Models.Song;

public sealed class Marker : ISerializable
{
    public string Text { get; set; } = string.Empty;
    public short Width { get; set; } // 0 - 565

    public Marker Clone() => new()
    {
        Text = Text,
        Width = Width
    };
}