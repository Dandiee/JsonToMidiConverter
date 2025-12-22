using System.Text.Json.Serialization;
using Dani.Data.Json.Converters;

namespace Dani.Data.Models.Enums;

[JsonConverter(typeof(TripletFeelConverter))]
public enum TripletFeel : byte
{
    None,

    Off,

    Eights,
    Sixteen,

    DottedEight,
    DottedSixteens,

    ScottishEight,
    ScottishSixteens
}