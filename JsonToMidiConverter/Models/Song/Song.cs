using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

public class SongRaw
{
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    
    [JsonPropertyName("parts")]
    public List<PartRaw> PartsRaw { get; set; } = [];
}