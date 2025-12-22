using System.Text.Json.Serialization;
using Dani.Data.Json.Converters;

namespace Dani.Data.Models.Enums;

[JsonConverter(typeof(AccentConverter))]
public enum Accent : byte
{
    None,

    Normal,
    Heavy,
}