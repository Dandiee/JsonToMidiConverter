using System.Text.Json.Serialization;
using Api.Generators;
using Api.Models.Converters;

namespace Api.Models;

[JsonConverter(typeof(TremoloBarConverter))]
public sealed class TremoloBar : InternalTremoloBar;

[AutoSerialize]
public partial class InternalTremoloBar : Serializable
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