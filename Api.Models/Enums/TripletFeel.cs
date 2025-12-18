using System.Text.Json.Serialization;
using Api.Models.Converters;

namespace Api.Models.Enums;

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