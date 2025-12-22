using System.Text.Json.Serialization;
using Dani.Data.Json.Converters;

namespace Dani.Data.Models.Enums;

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