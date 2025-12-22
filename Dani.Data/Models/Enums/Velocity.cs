using System.Text.Json.Serialization;

namespace Dani.Data.Models.Enums;

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