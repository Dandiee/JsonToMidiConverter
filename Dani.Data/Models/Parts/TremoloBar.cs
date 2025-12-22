using System.Text.Json.Serialization;
using Dani.Data.Generators;
using Dani.Data.Json.Converters;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

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