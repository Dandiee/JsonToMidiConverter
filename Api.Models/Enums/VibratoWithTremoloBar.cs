using System.Text.Json.Serialization;

namespace Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VibratoWithTremoloBar : byte
{
    None,

    Wide,
    Slight
}