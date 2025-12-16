using JsonToMidiConverter.Models.Song.Enums;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

public sealed class ChordStroke
{
    public StrokeTechnique Technique { get; set; } = StrokeTechnique.None;
    public int Duration { get; set; }
    public float StartTimeOffset { get; set; }
}