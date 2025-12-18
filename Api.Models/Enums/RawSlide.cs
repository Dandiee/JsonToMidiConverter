
using System.Text.Json.Serialization;

namespace Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RawSlide : byte
{
    None,

    Unknown,

    Below,
    Above,

    Upwards,
    Downwards,
    Shift,
    Legato,

    BelowUpwards,
    BelowDownwards,
    BelowShift,
    BelowLegato,

    AboveUpwards,
    AboveDownwards,
    AboveShift,
    AboveLegato,
}