using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Songs;

[AutoSerialize]
public partial class Author : Serializable
{
    public int PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public bool IsModerator { get; set; }
}