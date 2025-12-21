using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models.Parts;


[AutoSerialize]
public sealed partial class Voice : Serializable
{
    public List<Beat> Beats { get; set; } = [];

    public bool Rest { get; set; }
    public bool HasSameRhythm { get; set; }
}