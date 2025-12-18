using System.Text.Json.Serialization;

namespace Api.Models.Enums;

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