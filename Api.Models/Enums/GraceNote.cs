using System.Text.Json.Serialization;

namespace Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GraceNote : byte
{
    None,

    OnBeat,
    BeforeBeat
}