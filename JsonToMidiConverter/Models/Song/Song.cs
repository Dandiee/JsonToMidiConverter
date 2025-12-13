namespace JsonToMidiConverter.Models.Song;

public sealed partial class Song
{
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    public List<Part> Parts { get; set; } = [];

}