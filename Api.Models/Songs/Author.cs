using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models.Songs;

[AutoSerialize]
public partial class Author : Serializable
{
    public int PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public bool IsModerator { get; set; }
}