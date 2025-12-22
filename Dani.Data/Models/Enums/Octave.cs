using System.Text.Json.Serialization;
using Dani.Data.Json.Converters;

namespace Dani.Data.Models.Enums;

[JsonConverter(typeof(OctaveConverter))]
public enum Octave : byte
{
    None,

    Higher,
    Lower
}