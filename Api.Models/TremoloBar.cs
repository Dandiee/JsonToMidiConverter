using System.Text.Json.Serialization;
using Api.Models.Converters;

namespace Api.Models;

[JsonConverter(typeof(TremoloBarConverter))]
public sealed class TremoloBar
{
    public float Tone { get; set; }
    public List<Point> Points { get; set; } = [];

    public bool LegacyFlag { get; set; }

}