using System.Diagnostics;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Dani.Converter.Dumper;

[DebuggerDisplay("{EventIndex}")]
public sealed class TimedNoteEvent
{
    public int MeasureIndex { get; }
    public int EventIndex { get; }

    public long Start { get; }
    public long End { get; }
    public long Duration { get; }

    public NoteOnEvent On { get; }
    public NoteOffEvent Off { get; }

    public List<PitchBending> PitchBends { get; } = [];

    public bool IsFuckedUp { get; }

    public TimedNoteEvent(int measureIndex, int eventIndex, TimedEvent on, TimedEvent off, bool isSongsterSpecialPieceOfShit, List<TimedEvent> pitchBends)
    {
        MeasureIndex = measureIndex;
        EventIndex = eventIndex;
        Start = on.Time;
        End = off.Time;
        Duration = End - Start;
        On = (NoteOnEvent)on.Event;
        Off = (NoteOffEvent)off.Event;
        IsFuckedUp = isSongsterSpecialPieceOfShit;
        PitchBends = pitchBends.Select(e => new PitchBending(e.Time, ((PitchBendEvent)e.Event).PitchValue)).ToList();
    }


    public bool IsMatching(int channel, int noteNumber) => On.Channel == channel && On.NoteNumber == noteNumber;
}