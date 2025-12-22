using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class Point : Serializable
{
    public float Position { get; set; } // 0 - 60
    public float Tone { get; set; } // -800 - 600
    public byte Vibrato { get; set; } // TODO: not all types have vibrato
}