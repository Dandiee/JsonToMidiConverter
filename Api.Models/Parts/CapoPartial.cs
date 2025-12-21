using System.Text.Json.Serialization;
using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models;

[AutoSerialize]
public sealed partial class CapoPartial : Serializable
{
    public List<byte> Strings { get; set; } = [];
    public byte Fret { get; set; }
}