using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

public enum TremoloStyle : byte
{
    None,

    Slight,
    Wide,
    CustomGraph,
    Dip
}