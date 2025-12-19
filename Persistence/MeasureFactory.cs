using Api.Models;
using Persistence.Models;

namespace Persistence;

public static class MeasureFactory
{
    public static Measure FromRaw(RawMeasure raw)
    {
        var model = ThreadLocalPool<Measure>.Rent();


        model.Voices = raw.Voices.Select(VoiceFactory.FromRaw).ToList();
        model.Index = raw.Index;
        model.Signature = raw.Signature;
        model.AlternateEnding = raw.AlternateEnding;
        model.Marker = raw.Marker;
        model.Tempo = raw.Tempo;
        model.TripletFeel = raw.TripletFeel;
        model.Repeat = raw.Repeat;
        model.Rest = raw.Rest;
        model.RepeatStart = raw.RepeatStart;
        model.DoubleBarLine = raw.DoubleBarLine;

        return model;
    }
}