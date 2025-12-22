using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class CapoPartial : Serializable
{
    public List<byte> Strings { get; set; } = [];
    public byte Fret { get; set; }
}