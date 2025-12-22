using System.Text.Json.Serialization;

namespace Dani.Data.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Harmonic : byte
{
    None,

    Semi,
    Artificial,
    Pinch,
    Natural,
    Feedback,
    Tapped,
    Tap
}