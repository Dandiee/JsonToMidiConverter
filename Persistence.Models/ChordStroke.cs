using Persistence.Models.Enums;

namespace Persistence.Models;


public sealed class ChordStroke
{
    public StrokeTechnique Technique { get; set; }
    public short Duration { get; set; }
    public float StartTimeOffset { get; set; }
}