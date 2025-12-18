using System.Text.Json.Serialization;
using Api.Models.Converters;
using Api.Models.Enums;
using Api.Models.Song;

namespace Api.Models;

public sealed class Note
{
    [JsonConverter(typeof(AccentuatedConverter))] public float Accentuated { get; set; }

    [JsonPropertyName("string")] 
    public float StringNumber { get; set; }
    public float HarmonicFret { get; set; }
    public sbyte Fret { get; set; }



    public Slide Slide { get; set; }
    public Harmonic Harmonic { get; set; }
    public Velocity Velocity { get; set; }



    public List<ushort> Tremolo { get; set; } = [];
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