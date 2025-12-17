using JsonToMidiConverter.Models.Song.Enums;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

public sealed class ChordStroke : ISerializable
{
    public StrokeTechnique Technique { get; set; } = StrokeTechnique.None;
    public byte Duration { get; set; }
    public byte StartTimeOffset { get; set; }
}