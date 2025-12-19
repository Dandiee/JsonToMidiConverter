using Api.Generators;
using Api.Models.Enums;
using Api.Models.Serialization;

namespace Api.Models;

[AutoSerialize]
public sealed partial class ChordStroke : Serializable
{
    public StrokeTechnique Technique { get; set; }
    public short Duration { get; set; }
    public float StartTimeOffset { get; set; }
}