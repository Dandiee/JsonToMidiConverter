using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Models.Song.Enums;
using JsonToMidiConverter.Models.Song.JsonConverters;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("N{Index} B{Beat.Index} M{Measure.Index} P{Part.Index} STR{StringNumber}/FRT{Fret} NN{NoteNumber}")]
public sealed partial class Nota
{
    
    [JsonPropertyName("string")] public float StringNumber { get; set; }
    //[JsonIgnore] public float StringNumberData { get; set; }
    //
    //[JsonInclude, JsonPropertyName("string")]
    //private float LegacyStringNumber
    //{
    //    set => StringNumberData = (sbyte)Math.Round(value * 2);
    //}



    public sbyte Fret { get; set; }
    


    [JsonPropertyName("slide")] public RawSlide RawSlide { get; set; }

    public Velocity Velocity { get; set; }

    

    [JsonConverter(typeof(TremoloConverter))] public MusicalFraction? Tremolo { get; set; }


    // harmonic related shit
    public HarmonicData? HarmonicData { get; set; }
    
    public Harmonic Harmonic { get; set; }
    public float HarmonicFret { get; set; }

    //[JsonIgnore] public byte HarmonicFretIndex { get; set; }
    //[JsonInclude, JsonPropertyName("harmonicFret")]
    //private float LegacyHarmonicFret
    //{
    //    set => HarmonicFretIndex = (byte)Array.IndexOf(Extensions.HarmonicFretPalette, value);
    //}

    //[JsonInclude, JsonPropertyName("harmonicData")] private HarmonicData? LegacyHarmonicData
    //{
    //    set
    //    {
    //        if (value == null) return;

    //        // 1. Map the Type
    //        if (value.Type == "ah") Harmonic = Harmonic.Artificial;
    //        else if (value.Type == "th") Harmonic = Harmonic.Tapped;

    //        HarmonicFret = value.Shift switch
    //        {
    //            12 => 12.0f, // Octave (2nd Harmonic)
    //            19 => 7.0f,  // Octave + 5th (3rd Harmonic)
    //            24 => 5.0f,  // 2 Octaves (4th Harmonic)
    //            28 => 4.0f,  // 2 Octaves + Major 3rd (5th Harmonic)
    //            31 => 3.2f,  // 2 Octaves + 5th (6th Harmonic, approx fret 3.2)
    //            36 => 2.7f,  // 3 Octaves (8th Harmonic)
    //            _ => 12.0f  // Default fallback
    //        };
    //    }
    //}






    public Bend? Bend { get; set; }


    // accent
    [JsonConverter(typeof(AccentuatedConverter))] public float Accentuated { get; set; }
    //public Accent Accent { get; set; } = Accent.None;
    //[JsonInclude, JsonPropertyName("accentuated")]
    //[JsonConverter(typeof(AccentuatedConverter))] private float LegacyAccentuated { set => Accent = (Accent)(byte)value; }



    // vibrato stuff
    [JsonConverter(typeof(VibratoConverter))] public bool Vibrato { get; set; }
    [JsonConverter(typeof(VibratoConverter))] public bool WideVibrato { get; set; }

    //public NoteVibrato Vibrato { get; set; }
    //[JsonPropertyName("vibrato")] private bool LegacyVibrato {set { if (value) Vibrato = Enums.NoteVibrato.Slight; }}
    //[JsonPropertyName("wideVibrato")] private bool LegacyWideVibrato { set { if (value) Vibrato = Enums.NoteVibrato.Wide; } }



    public bool Trill { get; set; }
    public bool Hp { get; set; }
    //[JsonIgnore] public LegatoTechnique Legato { get; set; } = LegatoTechnique.None;
    //[JsonInclude, JsonPropertyName("hp")] private bool LegacyHp { set { if (value) Legato = LegatoTechnique.HammerPull; } }
    //
    //[JsonInclude, JsonPropertyName("trill")] private bool LegacyTrill { set { if (value) Legato = LegatoTechnique.Trill; } }


    public bool Grace { get; set; }
    
    public bool Tie { get; set; }
    public bool Rest { get; set; }
    public bool Staccato { get; set; }
    public bool Dead { get; set; }
    public bool Ghost { get; set; }

    public Nota Clone() => new()
    {
        Fret = Fret,
        StringNumber = StringNumber,
        RawSlide = RawSlide,
        Vibrato = Vibrato,
        Hp = Hp,
        Tie = Tie,
        Rest = Rest,
        Staccato = Staccato,
        Accentuated = Accentuated,
        Ghost = Ghost,
        Harmonic = Harmonic,
        HarmonicFret = HarmonicFret,
        Bend = Bend?.Clone(),
        Dead = Dead,
        Tremolo = Tremolo?.Copy(),
        WideVibrato = WideVibrato,
        Velocity = Velocity,
        Grace = Grace,
        HarmonicData = HarmonicData?.Clone(),
        Trill = Trill,
    };
}