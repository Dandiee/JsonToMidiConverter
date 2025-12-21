using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models;

[AutoSerialize]
public sealed partial class Record : Serializable
{
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Views { get; set; }
    public int PartCount { get; set; }
    public int PartFile { get; set; }
    public int PartFileOffset { get; set; }
}