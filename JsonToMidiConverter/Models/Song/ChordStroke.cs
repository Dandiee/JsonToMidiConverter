using JsonToMidiConverter.Models.Song.Enums;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

public sealed class ChordStroke : ISerializable
{
    public StrokeTechnique Technique { get; set; } = StrokeTechnique.None;
    public short Duration { get; set; } // 0 - 6
    public float StartTimeOffset { get; set; } // 0 - 100
}