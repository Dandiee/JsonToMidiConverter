using Dani.Converter.Models;
using Dani.Data.Models.Parts;

namespace Dani.Converter;

public static class PitchBendGenerator
{
    private const int MidiCenter = 8192;
    private const int PitchBendRangeSemitones = 12;
    private const float UnitsPerCent = 8192.0f / (PitchBendRangeSemitones * 100.0f);
    private const int PositionGridMax = 60;

    public static List<BendEvent> GenerateSlide(int noteDuration, int startFret, int endFret)
    {
        var events = new List<BendEvent>();

        // Sensitivity = 24 semitones. 1 Semitone = 341 units.
        int semitoneDiff = endFret - startFret;
        int targetShift = semitoneDiff * 341;

        // Slide Duration: Heuristic from log (950 ticks for a 7680 note). 
        // It seems to be a fast transition (Portamento).
        // Let's use a fixed transition time or a fraction of the note.
        int slideDuration = Math.Min(noteDuration, 960); // Cap at 960 ticks (quarter note at 960ppq)
        int step = 10; // High resolution from log

        for (int tick = 0; tick <= slideDuration; tick += step)
        {
            var progress = (float)tick / slideDuration;
            var value = (int)(8192 + (targetShift * progress));
            events.Add(new BendEvent { Tick = tick, Value = value });
        }

        // Hold final value?
        // Usually yes, until the next note starts.
        if (slideDuration < noteDuration)
        {
            events.Add(new BendEvent { Tick = noteDuration, Value = 8192 + targetShift });
        }

        return events;
    }


    public static List<BendEvent> GenerateBends(this Nota input, int actualNoteDuration)
    {
        var events = new List<BendEvent>();

        int baseDuration = (int)input.Duration.Tick;
        int tickResolution = baseDuration / PositionGridMax;
        if (tickResolution < 1) tickResolution = 1;

        var bendPoints = ParsePoints(input.Bend, baseDuration, tickResolution);
        var tremoloPoints = ParsePoints(input.Beat.Tremolo, baseDuration, tickResolution); // Tremolo uses same parsing logic

        int currentTick = 0;

        // Iterate through the full physical duration of the note
        while (currentTick <= actualNoteDuration)
        {
            // --- BEND LOGIC: LOOPING ---
            // Bends repeat if the note extends beyond the base duration.
            int bendTick = Math.Min(currentTick, baseDuration);  //currentTick % baseDuration;
            int bendValue = InterpolateValue(bendPoints, bendTick);
            int bendOffset = bendValue - MidiCenter;

            // --- TREMOLO LOGIC: ONE-SHOT (HOLD) ---
            // Tremolo plays once and then holds its last value.
            // We simply clamp the lookup tick to the Base Duration.
            int tremoloTick = Math.Min(currentTick, baseDuration);
            int tremoloValue = InterpolateValue(tremoloPoints, tremoloTick);
            int tremoloOffset = tremoloValue - MidiCenter;

            // --- SUMMING ---
            int combinedValue = Math.Clamp(MidiCenter + bendOffset + tremoloOffset, 0, 16383);

            events.Add(new BendEvent { Tick = currentTick, Value = combinedValue });

            currentTick += tickResolution;
        }

        return events;
    }

    // Reused for both Bend and TremoloBar
    private static List<BendPoint> ParsePoints(Bend data, int baseDuration, int resolution)
    {
        var points = new List<BendPoint>();

        if (data?.Points == null || data.Points.Count == 0)
        {
            points.Add(new BendPoint { Ticks = 0, Value = MidiCenter });
            points.Add(new BendPoint { Ticks = baseDuration, Value = MidiCenter });
            return points;
        }

        foreach (var p in data.Points)
        {
            int ticks = (int)p.Position * resolution;
            int shift = (int)Math.Round(p.Tone * UnitsPerCent);
            int value = Math.Clamp(MidiCenter + shift, 0, 16383);
            points.Add(new BendPoint { Ticks = ticks, Value = value });
        }

        var sorted = points.OrderBy(x => x.Ticks).ToList();

        if (sorted[0].Ticks > 0)
            sorted.Insert(0, new BendPoint { Ticks = 0, Value = MidiCenter });

        // Ensure we cover to the end of the base duration for interpolation
        if (sorted.Last().Ticks < baseDuration)
            sorted.Add(new BendPoint { Ticks = baseDuration, Value = sorted.Last().Value });

        return sorted;
    }

    private static int InterpolateValue(List<BendPoint> points, int tick)
    {
        BendPoint p1 = points[0];
        BendPoint p2 = points.Last();

        // Standard linear interpolation logic...
        for (int i = 0; i < points.Count - 1; i++)
        {
            if (tick >= points[i].Ticks && tick <= points[i + 1].Ticks)
            {
                p1 = points[i];
                p2 = points[i + 1];
                break;
            }
        }

        int run = p2.Ticks - p1.Ticks;
        if (run == 0) return p1.Value;

        var progress = (float)(tick - p1.Ticks) / run;
        return (int)(p1.Value + (p2.Value - p1.Value) * progress);
    }
}

// Data Structures
public struct BendPoint { public int Ticks; public int Value; }
public struct BendEvent { public int Tick; public int Value; }
