using Api.Models;
using Api.Models.Enums;
using Persistence.Models;
using Persistence.Models.Enums;

namespace Persistence;
public static class NoteFactory
{
    private static readonly IReadOnlyDictionary<HarmonicType, Harmonic> HarmonicTypeMapping =
        new Dictionary<HarmonicType, Harmonic>
        {
            [HarmonicType.None] = Harmonic.None,
            [HarmonicType.Ah] = Harmonic.Artificial,
            [HarmonicType.Th] = Harmonic.Tapped,
        };

    public static Note FromRaw(RawNote raw) => new()
    {
        Fret = raw.Fret,
        Slides = MapSlides(raw).ToList(),
        Velocity = raw.Velocity,
        Tremolo = MusicalFraction.Create(raw.Tremolo),
        Harmonic = MapHarmonic(raw),
        HarmonicFret = MapHarmonicFret(raw),
        Bend = MapBend(raw),
        Accentuated = raw.Accentuated,
        Vibrato = MapVibrato(raw),
        Legato = MapLegato(raw),
        Grace = MapGrace(raw),
        Rest = raw.Rest,
        Staccato = raw.Staccato,
        Dead = raw.Dead,
        Ghost = raw.Ghost,
        StringNumber = raw.StringNumber,
        Tie = raw.Tie
    };

    private static GraceNote MapGrace(RawNote raw)
    {
        if (raw.Grace) return GraceNote.OnBeat; // ??
        return GraceNote.None;
    }

    private static Legato MapLegato(RawNote raw)
    {
        if (raw.Trill) return Legato.Trill;
        if (raw.Hp) return Legato.HammerPull;

        return Legato.None;
    }

    private static Vibrato MapVibrato(RawNote raw)
    {
        if (raw.WideVibrato) return Vibrato.Wide;
        if (raw.Vibrato) return Vibrato.Standard;

        return Vibrato.None;
    }

    private static Bend? MapBend(RawNote raw)
    {
        if (raw.Bend == null) return null;

        return new Bend
        {
            Points = raw.Bend.Points,
            Tone = raw.Bend.Tone,
            Style = raw.Bend.LegacyFlag ? TremoloStyle.Dip : TremoloStyle.None
        };
    }

    private static float MapHarmonicFret(RawNote raw)
    {
        if (raw.HarmonicData == null) return raw.HarmonicFret;
        if (raw.HarmonicFret != 0) throw new NotSupportedException("Idk");

        return raw.HarmonicData.Shift switch
        {
            12 => 12.0f, // Octave (2nd Harmonic)
            19 => 7.0f,  // Octave + 5th (3rd Harmonic)
            24 => 5.0f,  // 2 Octaves (4th Harmonic)
            28 => 4.0f,  // 2 Octaves + Major 3rd (5th Harmonic)
            31 => 3.2f,  // 2 Octaves + 5th (6th Harmonic, approx fret 3.2)
            36 => 2.7f,  // 3 Octaves (8th Harmonic)
            _ => 12.0f  // Default fallback
        };
    }

    private static Harmonic MapHarmonic(RawNote raw)
    {
        if (raw.HarmonicData == null) return raw.Harmonic;
        if (raw.Harmonic != Harmonic.None) throw new NotSupportedException("Idk");

        return HarmonicTypeMapping[raw.HarmonicData.Type];
    }

    private static IEnumerable<Models.Enums.Slide> MapSlides(RawNote rawNote)
    {
        if (rawNote.RawSlide is Api.Models.Enums.RawSlide.Unknown or Api.Models.Enums.RawSlide.None)
            yield break;

        var rest = rawNote.RawSlide.ToString().ToLowerInvariant();

        if (rest.StartsWith("below"))
        {
            yield return Models.Enums.Slide.Below;
            rest = rest[5..];
        }
        else if (rest.StartsWith("above"))
        {
            yield return Models.Enums.Slide.Above;
            rest = rest[5..];
        }

        if (rest.Length > 0)
        {
            yield return rest switch
            {
                "upwards" => Models.Enums.Slide.Upwards,
                "downwards" => Models.Enums.Slide.Downwards,
                "shift" => Models.Enums.Slide.Shift,
                "legato" => Models.Enums.Slide.Legato,

                _ => throw new NotSupportedException()
            };
        }
    }
}

