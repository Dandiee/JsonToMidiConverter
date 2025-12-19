using Api.Generators;
using Api.Models.Enums;
using Api.Models.Serialization;

namespace Api.Models;

[AutoSerialize]
public sealed partial class Bend : Serializable
{
    public List<Point> Points { get; set; } = [];
    public TremoloStyle Style { get; set; }
    public short Tone { get; set; }
}