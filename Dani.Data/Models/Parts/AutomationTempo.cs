using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class AutomationTempo : Serializable
{
    public int Type { get; set; }
    public int Bpm { get; set; }
    public int Progressive { get; set; }

    public ushort Measure { get; set; }
    public float Position { get; set; }
    
    public bool Dotted { get; set; }
    public bool Visible { get; set; }
    public bool Linear { get; set; }

    public string Text { get; set; } = string.Empty;
}