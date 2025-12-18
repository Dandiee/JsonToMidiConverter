
using System.Text.Json.Serialization;

namespace Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HarmonicType : byte
{
    None,

    Ah, // Artificial Harmonic
    Th // Tapped Harmonic
}