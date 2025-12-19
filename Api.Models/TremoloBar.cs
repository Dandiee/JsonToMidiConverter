using System.Text.Json.Serialization;
using Api.Models.Converters;

namespace Api.Models;

[JsonConverter(typeof(TremoloBarConverter))]
public sealed class TremoloBar : InternalTremoloBar;

public class InternalTremoloBar
{
    public short Tone { get; set; }
    public List<Point> Points { get; set; } = [];
    public bool LegacyFlag { get; set; }

    internal TremoloBar ToModel() => new()
    {
        LegacyFlag = LegacyFlag,
        Points = Points,
        Tone = Tone,
    };
}