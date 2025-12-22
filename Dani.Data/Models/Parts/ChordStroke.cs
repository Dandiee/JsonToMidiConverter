using Dani.Data.Generators;
using Dani.Data.Models.Enums;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class ChordStroke : Serializable
{
    public StrokeTechnique Technique { get; set; }
    public short Duration { get; set; }
    public float StartTimeOffset { get; set; }
}