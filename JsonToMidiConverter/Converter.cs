using JsonToMidiConverter.Context;
using JsonToMidiConverter.Models.Song;
using JsonToMidiConverter.Test;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Microsoft.VisualBasic;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Xml.Linq;
using Slide = JsonToMidiConverter.Context.Slide;

namespace JsonToMidiConverter;

internal static class Converter
{
    public const int TicksPerQuarter = 15360;
    public const int TicksPer64Th = 960; // The "Magic Grid" unit
    public const int MsPer64Th = TicksPerQuarter / TicksPer64Th;


    // Standard MIDI Values
    private const ushort PitchBendCenter = 8192;

    public static readonly SevenBitNumber DefaultVelocity = 112.To7();
    public static List<(long AbsoluteTime, MidiEvent Event)>[] ReferenceData;
    public static readonly long StandardSlideStepSize = 960;

    public static MidiFile Convert(Song song, string referenceMidiPath)
    {
        ReferenceData = GetReferenceMidiData(referenceMidiPath);
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarter) };
        Time.Map = song.Parts[0].GetTempo(midiFile);
        midiFile.ReplaceTempoMap(Time.Map);
        song.Build(midiFile);

        foreach (var part in song.Parts)
        {
            var events = new Events();
            AddTrackHeader(events, part);

            foreach (var measure in part.Measures)
            {
                AddMeasureMarker(events, measure);

                foreach (var beat in measure.Beats)
                {
                    foreach (var note in beat.Notes.Where(e => !e.Rest))
                    {
                       

                        

                        var start = note.GetStartTime();
                        var end = note.GetEndTime();


                        if (note.Vibrato)
                        {
                            if (note.Slide != Slide.None)
                            {
                                var slide = note.GetSlide();

                                var note1Start = beat.AbsoluteBeatStartTime;
                                var note1End = note1Start + slide.HoldDuration;
                                var note1Pitch = note.NoteNumber;

                                var note2Start = note1End;
                                var note2End = note2Start + note.ActualDuration - slide.HoldDuration;
                                var note2Pitch = note1Pitch + slide.Direction * slide.Steps;

                                On(events, note, note1Pitch, note1Start, note1End);
                                On(events, note, note2Pitch, note2Start, note2End);

                                events.Add(new ControlChangeEvent(1.To7(), 64.To7()), note1Start, note);
                                events.Add(new ControlChangeEvent(1.To7(), 0.To7()), note1End, note);
                            }
                            else
                            {
                                On(events, note, note.NoteNumber, start, end);

                                events.Add(new ControlChangeEvent(1.To7(), 64.To7()), start, note);
                                events.Add(new ControlChangeEvent(1.To7(), 0.To7()), end, note);
                            }
                        }
                        else if (note.Slide != Slide.None)
                        {
                            var slide = note.GetSlide();
                            if (note.Is("N0 B3 M7 P0"))
                            {

                            }

                            if (!slide.IsStepped)
                            {
                                if (!note.Tie) // tie starts are not ties, attack note is playing already
                                {
                                    var startNote = note.GetStartTime();
                                    var endNote = note.GetEndTime();
                                    On(events, note, note.NoteNumber, startNote, endNote);
                                }
                                
                                end = start + note.ActualDuration;
                                var step = note.ActualDuration / 2d / 100d;
                                events.Add(new PitchBendEvent(PitchBendCenter), end - 960, note);
                                Enumerable.Range(1, 100).ToList().ForEach(i =>
                                {
                                    events.Add(new PitchBendEvent(PitchBendCenter), end - 960 + (i * step.Tick), note);
                                });
                            }
                            else
                            {
                                var holdFrom = start;
                                var holdTo = holdFrom + slide.HoldDuration - (note.Index * note.GetStrum().Tick / 2);

                                On(events, note, note.NoteNumber, holdFrom, holdTo);

                                for (var i = 0; i < slide.Steps; i++)
                                {
                                    var stepFrom = holdTo + slide.StepDuration * i - (i * 9 * note.Index);
                                    var stepTo = stepFrom + slide.StepDuration - 9 * note.Index;
                                    var stepNote = note.NoteNumber + slide.Direction * (i + 1);

                                    On(events, note, stepNote, stepFrom, stepTo);
                                }
                            }
                        }
                       // else if (note.Dead)
                       // {
                       //     var noteStart = note.GetStartTime();
                       //     var noteEnd = noteStart + 960 / 2;
                       //     On(events, note, note.NoteNumber, noteStart, noteEnd);
                       // }
                        else if (note.Bend != null)
                        {
                            var noteStart = note.GetStartTime();
                            var noteEnd = note.GetEndTime();
                            var noteDuration = noteEnd - noteStart;
                            var quarterDuration = noteDuration / 4;
                            var pitchBendStep = quarterDuration / 60;
                            var leftoverDuration = noteDuration - quarterDuration;


                            events.Add(new ControlChangeEvent(1.To7(), 110.To7()), start, note);
                            events.Add(new ControlChangeEvent(1.To7(), 0.To7()), end, note);

                            if (!note.Tie) // tie roots are not ties, attack note is already playing
                            {
                                On(events, note, note.NoteNumber, noteStart, noteEnd);
                            }

                            events.Add(new PitchBendEvent(PitchBendCenter), noteStart + 1, note);
                            Enumerable.Range(1, 61).ToList().ForEach(i =>
                            {
                                events.Add(new PitchBendEvent(PitchBendCenter), noteStart + pitchBendStep * i, note);
                            });
                            events.Add(new PitchBendEvent(PitchBendCenter), noteStart + pitchBendStep * 60 + 1, note);
                        }
                        else if (!note.Tie)
                        {
                            if (note.Is("N0 B5 M25 P0"))
                            {

                            }

                            var noteStart = note.GetStartTime();
                            var noteEnd = note.GetEndTime();
                            Debug.Assert(noteEnd != noteStart);
                            Debug.Assert(noteEnd > noteStart);


                            On(events, note, note.NoteNumber, noteStart, note.GetEndTime());
                        }


                    }
                }
            }

            Validate(events, part);
            midiFile.Chunks.Add(events.ToTrackChunk());
        }

        return midiFile;
    }

    public static void On(Events events, Nóta note, int noteNumber, Time from, Time to)
    {
        events.Add(new PitchBendEvent(PitchBendCenter), from, note);
        events.Add(new NoteOnEvent((SevenBitNumber)noteNumber, DefaultVelocity), from, note);
        events.Add(new NoteOffEvent((SevenBitNumber)noteNumber, DefaultVelocity), to, note);
    }

    public static void Validate(Events events, Part part)
    {
        var chunk = ReferenceData[part.PartId];

        var isStarted = false;
        for (var i = 0; i < chunk.Count; i++)
        {
            var referenceEvent = chunk[i];
            isStarted |= referenceEvent.Event.Is<MarkerEvent>();
            
            if (!isStarted) continue;
            if (referenceEvent.Event.DeltaTime < 11 && referenceEvent.Event.Is<PitchBendEvent>()) continue;
            if (referenceEvent.Event is MarkerEvent mark && mark.Text == "END_OF_VOICE") break;
            if (referenceEvent.Event.EventType == MidiEventType.PitchBend)
            {
                var pitchBendEventsCount = 0;
                for (; chunk[i + pitchBendEventsCount].Event.EventType == MidiEventType.PitchBend; pitchBendEventsCount++) ;
                if (pitchBendEventsCount > 10)
                {
                    i += pitchBendEventsCount - 1;
                    continue;
                }
            }
            if (referenceEvent.Event is ControlChangeEvent cc && cc.ControlNumber == 1) continue; // i have no fuckin clue how do you know if its a slight vibrato

            var cursor = i + 2;
            var time = referenceEvent.AbsoluteTime;
            var type = referenceEvent.Event.EventType;
            var partDetails = $"P{part.Index} {part.Name} - {part.Instrument}";

            var matchesByTime = events.Where(e => Math.Abs(e.Time - referenceEvent.AbsoluteTime) < 10).Where(e => e.Event.EventType == referenceEvent.Event.EventType).ToList();
            var closest = events.Where(e => e.Event.EventType == referenceEvent.Event.EventType).MinBy(e => Math.Abs(e.Time - referenceEvent.AbsoluteTime));
            var distance = closest.Time - referenceEvent.AbsoluteTime;

            if (matchesByTime.Count > 1)
            {
                if (referenceEvent.Event.Is<ChannelEvent>()) matchesByTime = matchesByTime.Where(e => e.Event.As<ChannelEvent>().Channel == referenceEvent.Event.As<ChannelEvent>().Channel).ToList();
                if (referenceEvent.Event.Is<NoteEvent>()) matchesByTime = matchesByTime.Where(e => e.Event.As<NoteEvent>().NoteNumber == referenceEvent.Event.As<NoteEvent>().NoteNumber).ToList();
                if (referenceEvent.Event.Is<ControlChangeEvent>()) matchesByTime = matchesByTime.Where(e => e.Event.As<ControlChangeEvent>().ControlValue == referenceEvent.Event.As<ControlChangeEvent>().ControlValue).ToList();
                if (referenceEvent.Event.Is<ControlChangeEvent>()) matchesByTime = matchesByTime.Where(e => e.Event.As<ControlChangeEvent>().ControlValue == referenceEvent.Event.As<ControlChangeEvent>().ControlValue).ToList();
            }

            var asd = matchesByTime.Select(e => new
            {
                IndexOf = events.TimedEvents.IndexOf(e),
                Itme = e
            }).ToList();

            var match = matchesByTime.Single();

            if (match.Is<NoteEvent>())
            {
                Debug.Assert(match.As<NoteEvent>().NoteNumber == referenceEvent.Event.As<NoteEvent>().NoteNumber);
            }

            if (match.Is<ChannelEvent>())
            {
                Debug.Assert(match.As<ChannelEvent>().Channel == referenceEvent.Event.As<ChannelEvent>().Channel);
            }

            if (match.Is<ControlChangeEvent>())
            {
                Debug.Assert(match.As<ControlChangeEvent>().ControlNumber == referenceEvent.Event.As<ControlChangeEvent>().ControlNumber);
                Debug.Assert(match.As<ControlChangeEvent>().ControlValue == referenceEvent.Event.As<ControlChangeEvent>().ControlValue);
            }

            if (match.Is<TextEvent>())
            {
                Debug.Assert(match.As<TextEvent>().Text == referenceEvent.Event.As<TextEvent>().Text);
            }

            if (match.Is<SetTempoEvent>())
            {
                Debug.Assert(match.As<SetTempoEvent>().MicrosecondsPerQuarterNote == referenceEvent.Event.As<SetTempoEvent>().MicrosecondsPerQuarterNote);
            }
        }
    }

    public static bool AreVerySame(TimedEvent lhs, TimedEvent rhs)
    {
        if (lhs.Event.EventType != rhs.Event.EventType) return false;
        if (lhs.Time != rhs.Time) return false;

        var props = lhs.Event.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var prop in props)
        {
            var lhsValue = prop.GetValue(lhs);
            var rhsValue = prop.GetValue(rhs);

            if (lhsValue.ToString() != rhsValue.ToString()) return false;
        }

        return true;
    }

    public static void AddMeasureMarker(Events events, Measure measure)
    {
        events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measure.StartTime, null, null, measure.Part.PartId);

        var measureChange = measure.Part.Automations.Tempo.SingleOrDefault(e => e.Measure == measure.Index);
        if (measureChange != null && measure.Index != 0)
        {
            var newTempo = Tempo.FromBeatsPerMinute(measureChange.Bpm).MicrosecondsPerQuarterNote;
            events.Add(new SetTempoEvent(newTempo), measure.StartTime, null, null, measure.Part.PartId);
        }

        if (measure.Index > 0 && measure.Signature.Length > 0)
        {
            events.Add(new TimeSignatureEvent(measure.SignatureNominator.Value, measure.SignatureDenominator.Value), measure.StartTime, null, null, measure.Part.PartId);
        }
    }

    public static void AddTrackHeader(Events events, Part part)
    {
        var timeZero = new Time();

        var channels = part.InstrumentId == 1024
            ? [9]
            : Enumerable.Range(0, 9).ToArray();

        foreach (var i in channels)
        {
            // Program Change
            events.Add(new ProgramChangeEvent(part.InstrumentId.To7()), timeZero, null, i, part.PartId);
        }

        foreach (var i in channels)
        {
            // Mod Wheel Reset
            events.Add(new ControlChangeEvent(1.To7(), 0.To7()), timeZero, null, i, part.PartId);
        }

        foreach (var i in channels)
        {
            // Pitch Bend Reset
            events.Add(new PitchBendEvent(8192), timeZero, null, i, part.PartId);
        }

        foreach (var i in channels)
        {
            // RPN Pitch Range Setup (Your 4 events)
            events.Add(new ControlChangeEvent(101.To7(), 0.To7()), timeZero, null, i, part.PartId);
            events.Add(new ControlChangeEvent(100.To7(), 0.To7()), timeZero, null, i, part.PartId);
            events.Add(new ControlChangeEvent(6.To7(), 24.To7()), timeZero, null, i, part.PartId);
            events.Add(new ControlChangeEvent(38.To7(), 0.To7()), timeZero, null, i, part.PartId);
        }


        if (!string.IsNullOrEmpty(part.Name))
        {
            events.Add(new SequenceTrackNameEvent(part.Name), timeZero, null, null, part.PartId);
        }

        if (!string.IsNullOrEmpty(part.Instrument))
        {
            events.Add(new InstrumentNameEvent(part.Instrument), timeZero,
                null, null, part.PartId);
        }
    }

    private static List<(long AbsoluteTime, MidiEvent Event)>[] GetReferenceMidiData(string referenceMidiFile)
    {
        var referenceMidi = MidiFile.Read(referenceMidiFile);
        var results = new List<(long AbsoluteTime, MidiEvent Event)>[referenceMidi.Chunks.Count];

        for (var i = 0; i < referenceMidi.Chunks.Count; i++)
        {
            results[i] = new List<(long AbsoluteTime, MidiEvent Event)>();
            var time = 0l;
            foreach (var midiEvent in (referenceMidi.Chunks[i] as TrackChunk)!.Events)
            {

                if (time == 0 && (midiEvent is TimeSignatureEvent || midiEvent is SetTempoEvent))
                {
                    continue;
                }

                time += midiEvent.DeltaTime;
                results[i].Add(new(time, midiEvent));

            }
        }

        return results;
    }


}