using Dani.Data.Generators;
using Dani.Data.Models.Enums;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class BrushStroke : Serializable
{
    public Direction Direction { get; set; }
    public short Duration { get; set; }
    public float Shift { get; set; }
}