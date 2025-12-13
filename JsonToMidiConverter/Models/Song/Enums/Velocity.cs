using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Velocity
{
    Ppp,
    Pp,
    P,
    Mp,
    Mf,
    F,
    Ff,
    Fff
}