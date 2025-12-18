using System.Text.Json.Serialization;
using Api.Models.Converters;

namespace Api.Models.Enums;

[JsonConverter(typeof(OctaveConverter))]
public enum Octave : byte
{
    None,

    Higher,
    Lower
}