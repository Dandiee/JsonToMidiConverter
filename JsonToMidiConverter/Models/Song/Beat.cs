using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
public partial class Beat
{
    public Nóta[] Notes { get; set; } = Array.Empty<Nóta>();
    public string Velocity { get; set; } = string.Empty;

    /// <summary>
    /// THIS IS ONYL FOR VISUAL REPRESENTATION ON THE MUSIC SHEET DONT LET IT CONFUSE YOU AGAIN!
    /// </summary>
    public int Type { get; set; }
    public bool PalmMute { get; set; }
    public int[] Duration { get; set; } = Array.Empty<int>();
    public byte Numerator => (byte)Duration[0];
    public byte Denominator => (byte)Duration[1];
    public bool BeamStart { get; set; }
    public bool BeamStop { get; set; }
    public bool Vibrato { get; set; }
    public Text? Text { get; set; }
    public bool LetRing { get; set; }

    /// <summary>
    ///  THIS IS ALSO JUST HERE TO FUCK WITH ME, IGNORE THEFUCKER
    /// </summary>
    public int Dots { get; set; }
    public bool Rest { get; set; }
    public bool Tapping { get; set; }
    public int Tuplet { get; set; }
    public bool TupletStart { get; set; }
    public bool TupletStop { get; set; }
    public string? GraceNote { get; set; }
}