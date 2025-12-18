namespace Persistence.Models;

public sealed class Voice
{
    public List<Beat> Beats { get; set; } = [];

    public bool Rest { get; set; }
    public bool HasSameRhythm { get; set; }
}