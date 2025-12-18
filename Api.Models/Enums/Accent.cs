using Api.Models.Converters;
using System.Text.Json.Serialization;

namespace Api.Models.Enums;

[JsonConverter(typeof(AccentConverter))]
public enum Accent : byte
{
    None,

    Normal,
    Heavy,
}