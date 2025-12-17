using JsonToMidiConverter.Models.Song.Enums;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

public sealed class ChordStroke : ISerializable
{
    public StrokeTechnique Technique { get; set; } = StrokeTechnique.None;
    public double Duration { get; set; }
    public double StartTimeOffset { get; set; }
}