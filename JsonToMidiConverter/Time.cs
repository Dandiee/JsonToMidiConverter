using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace JsonToMidiConverter;

[DebuggerDisplay("{Tick} - {Span}")]
public readonly struct Time : IEquatable<Time>
{
    public static TempoMap Map;

    public readonly MusicalTimeSpan Span => TimeConverter.ConvertTo<MusicalTimeSpan>(Tick, Map);

    public readonly long Tick;

    public Time(long tick)
    {
        Tick = tick;
        if (Map == null) throw new Exception("I need a map.");
    }

    private Time(ITimeSpan timeSPan) : this(TimeConverter.ConvertFrom(timeSPan, Map)) { }

    public override string ToString() => $"{Tick} - {Span}";

    public Time(long bars, float beats) : this(new BarBeatFractionTimeSpan(bars, beats)) { }

    public Time(long numerator, long denominator)
    {
        if (numerator == 0 && denominator == 0)
        {
            Tick = 0;
        }
        else
        {
            Tick = TimeConverter.ConvertFrom(new MusicalTimeSpan(numerator, denominator), Map);
        }
    } 
    public Time() : this(0) { }

    public Time ApplyDots(int dots) => new((long)(Tick * (2 - 1 / Math.Pow(2, dots))));

    public static Time operator +(Time lhs, Time rhs) => new(lhs.Tick + rhs.Tick);
    public static Time operator -(Time lhs, Time rhs) => new(lhs.Tick - rhs.Tick);
    public static Time operator +(Time lhs, long rhs) => new(lhs.Tick + rhs);
    public static Time operator -(Time lhs, long rhs) => new(lhs.Tick - rhs);
    public static Time operator +(Time lhs, ITimeSpan rhs) => new(lhs.Tick + TimeConverter.ConvertFrom(rhs, Map));
    public static Time operator -(Time lhs, ITimeSpan rhs) => new(lhs.Tick - TimeConverter.ConvertFrom(rhs, Map));
    public static Time operator +(ITimeSpan lhs, Time rhs) => new(TimeConverter.ConvertFrom(lhs, Map) + rhs.Tick);
    public static Time operator -(ITimeSpan lhs, Time rhs) => new(TimeConverter.ConvertFrom(lhs, Map) - rhs.Tick);

    public static long operator %(Time lhs, long rhs) => lhs.Tick % rhs;


    public static Time operator *(Time lhs, float rhs) => new((long)(lhs.Tick * rhs));
    public static Time operator /(Time lhs, float rhs) => new((long)(lhs.Tick / rhs));

    public static Time operator *(Time lhs, long rhs) => new(lhs.Tick * rhs);
    public static Time operator /(Time lhs, long rhs) => new(lhs.Tick / rhs);

    public static Time operator *(long lhs, Time rhs) => rhs * lhs;
    public static Time operator /(long lhs, Time rhs) => rhs / lhs;

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