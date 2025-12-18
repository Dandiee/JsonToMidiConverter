namespace Api.Models;

public sealed class Song
{
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    public List<Part> Parts { get; set; } = [];
}