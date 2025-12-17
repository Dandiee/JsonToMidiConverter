using System.Text.Json.Serialization;
using JsonToMidiConverter.Models.Song.Enums;

namespace JsonToMidiConverter.Models.Song;

public sealed class Bend : ISerializable
{
    public TremoloStyle Style { get; set; } = TremoloStyle.CustomGraph;

    public double Tone { get; set; }

    public List<BasePoint> Points { get; set; } = new();

    [JsonInclude, JsonPropertyName("legacyFlag")]
    private bool LegacyFlagReader { set { if (value) Style = TremoloStyle.Dip; } }

    public Bend Clone() => new()
    {
        Style = Style,
        Tone = Tone,
        Points = Points.Select(e => e.Clone()).ToList()
    };

    public static Bend CreateDip() => new() { LegacyFlagReader = true };
}