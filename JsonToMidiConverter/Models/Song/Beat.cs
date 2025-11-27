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
    public int UpStroke { get; set; }
    public int DownStroke { get; set; }
    public Chord Chord { get; set; }
    public bool Slapping { get; set; }
    public bool Popping { get; set; }
    public string GradualVelocity { get; set; }
}

public sealed class Chord
{
    public string Text { get; set; }
    public double Width { get; set; }
}