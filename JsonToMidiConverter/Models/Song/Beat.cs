using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
public partial class Beat
{
    public Nóta[] notes { get; set; } = Array.Empty<Nóta>();
    public string velocity { get; set; } = string.Empty;

    /// <summary>
    /// THIS IS ONYL FOR VISUAL REPRESENTATION ON THE MUSIC SHEET DONT LET IT CONFUSE YOU AGAIN!
    /// </summary>
    public int type { get; set; }
    public bool palmMute { get; set; }
    public int[] duration { get; set; } = Array.Empty<int>();
    public byte numerator => (byte)duration[0];
    public byte denominator => (byte)duration[1];
    public bool beamStart { get; set; }
    public bool beamStop { get; set; }
    public bool vibrato { get; set; }
    public Text? text { get; set; }
    public bool letRing { get; set; }

    /// <summary>
    ///  THIS IS ALSO JUST HERE TO FUCK WITH ME, IGNORE THEFUCKER
    /// </summary>
    public int dots { get; set; }
    public bool rest { get; set; }
    public bool tapping { get; set; }
    public int tuplet { get; set; }
    public bool tupletStart { get; set; }
    public bool tupletStop { get; set; }
    public string? graceNote { get; set; }
}