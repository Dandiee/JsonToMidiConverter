namespace JsonToMidiConverter.Models.Song;

public sealed partial class Song
{
    public int songId { get; set; }
    public int revisionId { get; set; }
    public Part[] parts { get; set; } = Array.Empty<Part>();

}