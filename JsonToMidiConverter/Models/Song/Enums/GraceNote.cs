using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GraceNote : byte
{
    OnBeat,
    BeforeBeat
}