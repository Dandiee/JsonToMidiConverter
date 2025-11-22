namespace JsonToMidiConverter.Models;


public class RecordModel
{
    public int SongId { get; set; }
    public int ArtistId { get; set; }
    public int RevisionId { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public int Views { get; set; }
    public int Parts { get; set; }
}
