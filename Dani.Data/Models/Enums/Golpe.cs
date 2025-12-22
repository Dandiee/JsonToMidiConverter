using System.Text.Json.Serialization;

namespace Dani.Data.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Golpe : byte
{
    None,

    Finger,
    Thumb,
}