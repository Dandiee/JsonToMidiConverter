using System.Text.Json.Serialization;

namespace Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Velocity : byte
{
    None,

    Ppp,
    Pp,
    P,
    Mp,
    Mf,
    F,
    Ff,
    Fff
}