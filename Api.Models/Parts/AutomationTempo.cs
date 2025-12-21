using Api.Generators;

namespace Api.Models;

[AutoSerialize]
public sealed partial class AutomationTempo : MeasureTempo
{
    public ushort Measure { get; set; }
    public float Position { get; set; }
    
    public bool Dotted { get; set; }
    public bool Visible { get; set; }
    public bool Linear { get; set; }

    public string Text { get; set; } = string.Empty;
}