using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Harmonic
{
    Semi,
    Artificial,
    Pinch,
    Natural,
    Feedback,
    Tapped,
    Tap
}