using Api.Models;
using Persistence.Models;

namespace Persistence;

public static class MeasureFactory
{
    public static Measure FromRaw(RawMeasure raw) => new()
    {
        // Must map generic type (RawVoice -> Voice), so we create a new container
        Voices = raw.Voices.Select(VoiceFactory.FromRaw).ToList(),

        // Keeping Direct References (No hard copy)
        Signature = raw.Signature,
        AlternateEnding = raw.AlternateEnding,

        // Direct Object References
        Marker = raw.Marker,
        Tempo = raw.Tempo,

        // Primitives
        TripletFeel = raw.TripletFeel,
        Repeat = raw.Repeat,
        Rest = raw.Rest,
        RepeatStart = raw.RepeatStart,
        DoubleBarLine = raw.DoubleBarLine
    };
}