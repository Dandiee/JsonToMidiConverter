using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Songs;

[AutoSerialize]
public partial class AudioMeta : Serializable
{
    public bool HasQuickMix { get; set; }
    public bool HasMixLayout { get; set; }
}