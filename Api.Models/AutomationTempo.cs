namespace Api.Models;

public sealed class AutomationTempo : MeasureTempo
{
    public ushort Measure { get; set; }
    public float Position { get; set; }
    public bool Visible { get; set; }
    public bool Linear { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool Dotted { get; set; }
}