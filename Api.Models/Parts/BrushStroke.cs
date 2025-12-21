using Api.Generators;
using Api.Models.Enums;
using Api.Models.Serialization;

namespace Api.Models;

[AutoSerialize]
public sealed partial class BrushStroke : Serializable
{
    public Direction Direction { get; set; }
    public short Duration { get; set; }
    public float Shift { get; set; }
}