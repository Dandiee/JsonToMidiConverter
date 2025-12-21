using System.Text.Json.Serialization;
using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models;

[AutoSerialize]
public sealed partial class Song : Serializable
{
    public int SongId { get; set; }
    public int RevisionId { get; set; }
    public List<Part> Parts { get; set; } = [];
}