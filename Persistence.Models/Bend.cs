using Api.Models;
using Persistence.Models.Enums;

namespace Persistence.Models;

public sealed class Bend
{
    public List<Point> Points { get; set; } = [];
    public TremoloStyle Style { get; set; }
    public short Tone { get; set; }
}