using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RawSlide : byte
{

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