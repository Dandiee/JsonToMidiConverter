using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("V{Index} M{Measure.Index} P{Part.Index}")]
public class VoiceRaw
{
    public bool Rest { get; set; }
    
    [JsonPropertyName("beats")]
    public List<BeatRaw> BeatsRaw { get; set; } = [];
    public bool HasSameRhythm { get; set; }

    public VoiceRaw Clone() => new()
    {
        Rest = Rest,
        BeatsRaw = BeatsRaw.Select(b => b.Clone()).ToList(),
        HasSameRhythm = HasSameRhythm
    };
}