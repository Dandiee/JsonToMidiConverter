using Dani.Data.Generators;
using Dani.Data.Models.Enums;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class Bend : Serializable
{
    public List<Point> Points { get; set; } = [];
    public TremoloStyle Style { get; set; }
    public short Tone { get; set; }
}