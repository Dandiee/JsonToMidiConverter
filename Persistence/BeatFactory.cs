using Api.Models;
using Api.Models.Enums;
using Persistence.Models;
using Persistence.Models.Enums;

namespace Persistence;

public static class BeatFactory
{
    public static Beat FromRaw(RawBeat raw)
    {
        var (stroke, pickDirection) = MapStrokeAndDirection(raw);
        var tremolo = MapTremolo(raw);

        var model = ThreadLocalPool<Beat>.Rent();

        //model.Notes = raw.Notes.Select(NoteFactory.FromRaw).ToList();

        model.PalmMute = raw.PalmMute;
        model.LetRing = raw.LetRing;
        model.Rest = raw.Rest;

        // Logic Extractions
        model.Duration = raw.Duration;
        model.Harmonic = MapHarmonic(raw);
        model.Vibrato = MapVibrato(raw);
        model.Technique = MapTechnique(raw);
        model.Dots = MapDots(raw);
        model.GradualVelocity = raw.FadeIn ? GradualVelocity.Crescendo : raw.GradualVelocity;

        // Complex Spanner Logic
        model.BeamSpan = MapSpanner(raw.BeamStart, raw.BeamStop);
        model.TupletSpan = MapSpanner(raw.TupletStart, raw.TupletStop);
        model.TupletDenominator = raw.Tuplet > 1 ? raw.Tuplet : (byte)0;

        // Octave String Parsing
        model.Octave = raw.OctaveClef;
        // Calculated complex objects
        model.Stroke = stroke;
        model.PickDirection = pickDirection;
        model.Tremolo = tremolo;

        return model;
    }

    private static Harmonic MapHarmonic(RawBeat raw)
    {
        if (raw.Harmonic) return Harmonic.Natural;
        if (raw.ArtificialHarmonic) return Harmonic.Artificial;
        if (raw.PinchHarmonic) return Harmonic.Pinch;
        if (raw.SemiHarmonic) return Harmonic.Semi;
        if (raw.TapHarmonic) return Harmonic.Tapped;

        return Harmonic.None;
    }

    private static Vibrato MapVibrato(RawBeat raw)
    {
        if (raw.WideVibrato) return Vibrato.Wide;
        if (raw.Vibrato) return Vibrato.Standard;

        return Vibrato.None;
    }

    private static Technique MapTechnique(RawBeat raw)
    {
        if (raw.Slapping) return Technique.Slap;
        if (raw.Popping) return Technique.Pop;
        if (raw.Tapping) return Technique.Tap;

        return Technique.None;
    }

    private static Dot MapDots(RawBeat raw)
    {
        if (raw.DoubleDotted) return Dot.Double;
        if (raw.Dotted) return Dot.Single;

        return Dot.None;
    }

    private static Spanner MapSpanner(bool start, bool stop)
    {
        if (start) return Spanner.Start;
        if (stop) return Spanner.Stop;

        return Spanner.None;
    }

    private static Bend? MapTremolo(RawBeat raw)
    {
        // Direct object legacy mapping
        if (raw.TremoloBar != null)
            return new Bend
            {
                Points = raw.TremoloBar.Points,
                Tone = raw.TremoloBar.Tone,
                Style = TremoloStyle.CustomGraph
            };

        // Calculated legacy mapping
        var bend = new Bend();
        bool active = false;

        if (raw.VibratoBar > 0 || raw.VibratoWithTremoloBar == VibratoWithTremoloBar.Slight)
        {
            bend.Style = TremoloStyle.Slight;
            active = true;
        }
        else if (raw.WideVibratoBar > 0 || raw.VibratoWithTremoloBar == VibratoWithTremoloBar.Wide)
        {
            bend.Style = TremoloStyle.Wide;
            active = true;
        }

        return active ? bend : null;
    }

    // Returns a Tuple because Legacy logic affected both Stroke AND PickDirection
    private static (ChordStroke? Stroke, Direction PickDirection) MapStrokeAndDirection(RawBeat raw)
    {
        var stroke = ThreadLocalPool<ChordStroke>.Rent();
        var direction = Direction.None;
        bool hasStroke = false;

        // 1. BrushStroke Object Logic
        if (raw.BrushStroke != null)
        {
            hasStroke = true;
            if (raw.BrushStroke.Direction != Direction.None)
                direction = raw.BrushStroke.Direction;

            stroke.Duration = raw.BrushStroke.Duration;
            stroke.StartTimeOffset = raw.BrushStroke.Shift;
        }

        // 2. Arpeggio Object Logic
        if (raw.Arpeggio != null)
        {
            hasStroke = true;
            stroke.Technique = StrokeTechnique.Arpeggio;
            if (raw.Arpeggio.Direction != Direction.None)
                direction = raw.Arpeggio.Direction;

            stroke.Duration = raw.Arpeggio.Duration;
            stroke.StartTimeOffset = raw.Arpeggio.Shift;
        }

        // 3. Byte-based Legacy Logic (Rasgueado, Up/Down Stroke/Arpeggio)
        if (raw.HasRasgueado)
        {
            hasStroke = true;
            stroke.Technique = StrokeTechnique.Rasgueado;
        }

        // Helper for the byte mappings
        void ApplyByteLegacy(byte val, Direction dir, StrokeTechnique tech)
        {
            if (val > 0)
            {
                hasStroke = true;
                direction = dir;
                stroke.Technique = tech;
                stroke.Duration = val;
            }
        }

        ApplyByteLegacy(raw.UpStroke, Direction.Up, StrokeTechnique.None);
        ApplyByteLegacy(raw.DownStroke, Direction.Down, StrokeTechnique.None);
        ApplyByteLegacy(raw.UpArpeggio, Direction.Up, StrokeTechnique.Arpeggio);
        ApplyByteLegacy(raw.DownArpeggio, Direction.Down, StrokeTechnique.Arpeggio);

        return (hasStroke ? stroke : null, direction);
    }
}

