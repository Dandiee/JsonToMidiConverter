using System.Text.Json.Serialization;

namespace Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GradualVelocity : byte
{
    None,

    Crescendo,
    Decrescendo
}