using Api.Generators;
using Api.Models.Enums;
using Api.Models.Serialization;

namespace Api.Models.Parts;

[AutoSerialize]
public sealed partial class HarmonicData : Serializable
{
    public HarmonicType Type { get; set; }
    public string Note { get; set; } = string.Empty;
    public byte Shift { get; set; }
    public sbyte Fret { get; set; }
}