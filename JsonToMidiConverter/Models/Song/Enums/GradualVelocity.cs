using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GradualVelocity : byte
{
    Unset,

    Crescendo,
    Decrescendo
}