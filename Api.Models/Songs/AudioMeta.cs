using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models.Songs;

[AutoSerialize]
public partial class AudioMeta : Serializable
{
    public bool HasQuickMix { get; set; }
    public bool HasMixLayout { get; set; }
}