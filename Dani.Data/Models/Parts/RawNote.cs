using System.Text.Json.Serialization;
using Dani.Data.Json.Converters;
using Dani.Data.Models.Enums;

namespace Dani.Data.Models.Parts;

public sealed class RawNote
{
    public Accent Accentuated { get; set; }

    [JsonPropertyName("string")] 
    public float StringNumber { get; set; }
    public float HarmonicFret { get; set; }
    public sbyte Fret { get; set; }

    public Harmonic Harmonic { get; set; }
    public Velocity Velocity { get; set; }
    public RawSlide Slide { get; set; }


    public string LeftFingering { get; set; } = string.Empty;


    public MusicalFraction Tremolo { get; set; } = MusicalFraction.Zero;
    public HarmonicData? HarmonicData { get; set; }
    public TremoloBar? Bend { get; set; }



    [JsonConverter(typeof(VibratoConverter))] public bool Vibrato { get; set; }
    [JsonConverter(typeof(VibratoConverter))] public bool WideVibrato { get; set; }


    public bool Grace { get; set; }
    public bool Trill { get; set; }
    public bool Hp { get; set; }
    public bool Tie { get; set; }
    public bool Rest { get; set; }
    public bool Staccato { get; set; }
    public bool Dead { get; set; }
    public bool Ghost { get; set; }
}