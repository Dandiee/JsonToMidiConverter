

using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter;

public static class Extensions
{
    public static readonly IReadOnlyDictionary<Type, TimeSpanType> TimeSpanTypeMapping =
        new Dictionary<Type, TimeSpanType>
        {
            [typeof(BarBeatFractionTimeSpan)] = TimeSpanType.BarBeatFraction,
            [typeof(BarBeatTicksTimeSpan)] = TimeSpanType.BarBeatTicks,
            [typeof(MetricTimeSpan)] = TimeSpanType.Metric,
            [typeof(MidiTimeSpan)] = TimeSpanType.Midi,
            [typeof(MusicalTimeSpan)] = TimeSpanType.Musical,
        };

    public static TTimeSpan AddTicks<TTimeSpan>(this TTimeSpan time, long tick, TempoMap tempoMap)
        where TTimeSpan : ITimeSpan
    {
        var clone = time.Clone();
        var tickTime = TimeConverter.ConvertTo(Math.Abs(tick), TimeSpanTypeMapping[time.GetType()], tempoMap);

        if (tick >= 0)
        {
            clone = clone.Add(tickTime, TimeSpanMode.TimeLength);
        }
        else
        {
            clone = clone.Subtract(tickTime, TimeSpanMode.TimeLength);
        }

            return (TTimeSpan)clone;
    }
}