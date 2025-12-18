using System.Text.Json.Serialization;

namespace Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Golpe : byte
{
    None,

    Finger,
    Thumb,
}