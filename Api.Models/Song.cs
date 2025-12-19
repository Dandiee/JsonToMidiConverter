using Api.Generators;

namespace Api.Models;

[AutoSerialize]
public sealed partial class Song : Serializable
{
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    public List<RawPart> Parts { get; set; } = [];
}