namespace JsonToMidiConverter.Models.Song;

public sealed partial class Song
{
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    public Part[] Parts { get; set; } = Array.Empty<Part>();

}