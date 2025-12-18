using System.Text.Json.Serialization;
using Api.Models.Converters;

namespace Api.Models.Enums;

[JsonConverter(typeof(PickStrokeConverter))]
public enum PickStroke : byte
{
    None,

    Up,
    Down,

    // fr...
    True,
    False 
}