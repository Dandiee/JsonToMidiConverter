using System.Diagnostics;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter;

[DebuggerDisplay("{Tick} - {Span}")]
public readonly struct Time : IEquatable<Time>
{
    public static TempoMap Map;

    public readonly MusicalTimeSpan Span;

    public readonly long Tick;

    public Time(ITimeSpan timeSpan)
    {
        Tick = TimeConverter.ConvertFrom(timeSpan, Map);
        Span = TimeConverter.ConvertTo<MusicalTimeSpan>(Tick, Map);
        if (Map == null) throw new Exception("I need a map.");
    }

    public override string ToString() => $"{Tick} - {Span}";

    public Time(long bars, double beats) : this(new BarBeatFractionTimeSpan(bars, beats)) { }
    public Time(long numerator, long denominator) : this(new MusicalTimeSpan(numerator, denominator)) { }
    public Time(long tick) : this(TimeConverter.ConvertTo<MusicalTimeSpan>(tick, Map)) { }
    public Time() : this(0) { }

    public static Time operator +(Time lhs, Time rhs) => new(lhs.Tick + rhs.Tick);
    public static Time operator -(Time lhs, Time rhs) => new(lhs.Tick - rhs.Tick);
    public static Time operator +(Time lhs, long rhs) => new(lhs.Tick + rhs);
    public static Time operator -(Time lhs, long rhs) => new(lhs.Tick - rhs);
    public static Time operator +(Time lhs, ITimeSpan rhs) => new(lhs.Tick + TimeConverter.ConvertFrom(rhs, Map));
    public static Time operator -(Time lhs, ITimeSpan rhs) => new(lhs.Tick - TimeConverter.ConvertFrom(rhs, Map));
    public static Time operator +(ITimeSpan lhs, Time rhs) => new(TimeConverter.ConvertFrom(lhs, Map) + rhs.Tick);
    public static Time operator -(ITimeSpan lhs, Time rhs) => new(TimeConverter.ConvertFrom(lhs, Map) - rhs.Tick);

    public static long operator %(Time lhs, long rhs) => lhs.Tick % rhs;


    public static Time operator *(Time lhs, double rhs) => new((long)(lhs.Tick * rhs));
    public static Time operator /(Time lhs, double rhs) => new((long)(lhs.Tick / rhs));

    public static Time operator *(Time lhs, long rhs) => new(lhs.Tick * rhs);
    public static Time operator /(Time lhs, long rhs) => new(lhs.Tick / rhs);

    public static bool operator <(Time lhs, Time rhs) => lhs.Tick < rhs.Tick;
    public static bool operator >(Time lhs, Time rhs) => lhs.Tick > rhs.Tick;

    public static bool operator <=(Time lhs, Time rhs) => lhs.Tick <= rhs.Tick;
    public static bool operator >=(Time lhs, Time rhs) => lhs.Tick >= rhs.Tick;

    public static bool operator ==(Time lhs, Time rhs) => lhs.Tick == rhs.Tick;
    public static bool operator !=(Time lhs, Time rhs) => lhs.Tick != rhs.Tick;

    public Time Clone() => new(Tick);

    public bool Equals(Time other) => Span.Equals(other.Span) && Tick == other.Tick;
    public override bool Equals(object? obj) => obj is Time other && Equals(other);
    public override int GetHashCode() => Tick.GetHashCode();
}