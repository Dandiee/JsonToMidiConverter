using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Songs;

[AutoSerialize]
public partial class Video : Serializable
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string VideoId { get; set; } = string.Empty;
}