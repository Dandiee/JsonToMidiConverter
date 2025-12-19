using Api.Models;
using Api.Models.Enums;
using Persistence.Models;
using Persistence.Models.Enums;

namespace Persistence;
public static class NoteFactory
{

    private static readonly float[] HarmonicFretTable =
    [
        -1f, 0f, 1f, 2f, 2.4f, 2.7f, 3f, 3.2f, 4f, 4.4f, 4.7f, 5f,
        5.2f, 5.7f, 5.8f, 6f, 6.2f, 7f, 8f, 8.2f, 8.4f, 9f, 9.6f,
        10f, 11f, 11.8f, 12f, 13f, 14f, 14.7f, 15f, 16f, 17f, 18f,
        19f, 19.6f, 20f, 21f, 21.7f, 22f, 23f, 24f, 26f, 28f, 29f,
        35f, 40f
    ];

    private static readonly Dictionary<float, byte> HarmonicFretLookup = HarmonicFretTable
        .Select((fret, index) => (fret, index))
        .ToDictionary(pair => pair.fret, pair => (byte)pair.index);


    private static readonly IReadOnlyDictionary<HarmonicType, Harmonic> HarmonicTypeMapping =
        new Dictionary<HarmonicType, Harmonic>
        {
            [HarmonicType.None] = Harmonic.None,
            [HarmonicType.Ah] = Harmonic.Artificial,
            [HarmonicType.Th] = Harmonic.Tapped,
        };

    public static Note FromRaw(RawNote raw)
    {
        var model = ThreadLocalPool<Note>.Rent();

        model.Fret = raw.Fret;
        model.Slides = MapSlides(raw);
        model.Tremolo = raw.Tremolo;
        model.Bend = MapBend(raw);
        model.Velocity = raw.Velocity;
        model.Harmonic = MapHarmonic(raw);
        model.HarmonicFretIndex = HarmonicFretLookup[MapHarmonicFret(raw)];
        model.Accentuated = raw.Accentuated;
        model.Vibrato = MapVibrato(raw);
        model.Legato = MapLegato(raw);
        model.Grace = MapGrace(raw);
        model.Rest = raw.Rest;
        model.Staccato = raw.Staccato;
        model.Dead = raw.Dead;
        model.Ghost = raw.Ghost;
        model.DoubledString = (sbyte)(raw.StringNumber * 2);
        model.Tie = raw.Tie;
        // model.LeftFingering = raw.LeftFingering.Equals("T", StringComparison.CurrentCultureIgnoreCase);

        return model;
    }

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
        if (raw.HarmonicFret != 0) return raw.HarmonicFret;

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
        if (raw.Harmonic != Harmonic.None) return raw.Harmonic;

        return HarmonicTypeMapping[raw.HarmonicData.Type];
    }

    private static SlideFlags MapSlides(RawNote rawNote)
        => rawNote.Slide switch
        {
            // 1. Standalone Origins
            RawSlide.Below => SlideFlags.FromBelow,
            RawSlide.Above => SlideFlags.FromAbove,

            // 2. Standalone Motions
            RawSlide.Upwards => SlideFlags.Upwards,
            RawSlide.Downwards => SlideFlags.Downwards,
            RawSlide.Shift => SlideFlags.Shift,
            RawSlide.Legato => SlideFlags.Legato,

            // 3. The "Below" Combinations
            RawSlide.BelowUpwards => SlideFlags.FromBelow | SlideFlags.Upwards,
            RawSlide.BelowDownwards => SlideFlags.FromBelow | SlideFlags.Downwards,
            RawSlide.BelowShift => SlideFlags.FromBelow | SlideFlags.Shift,
            RawSlide.BelowLegato => SlideFlags.FromBelow | SlideFlags.Legato,

            // 4. The "Above" Combinations
            RawSlide.AboveUpwards => SlideFlags.FromAbove | SlideFlags.Upwards,
            RawSlide.AboveDownwards => SlideFlags.FromAbove | SlideFlags.Downwards,
            RawSlide.AboveShift => SlideFlags.FromAbove | SlideFlags.Shift,
            RawSlide.AboveLegato => SlideFlags.FromAbove | SlideFlags.Legato,

            // 5. Defaults
            _ => SlideFlags.None
        };
}

