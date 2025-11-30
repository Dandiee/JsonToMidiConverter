using JsonToMidiConverter.Models;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Slide = JsonToMidiConverter.Context.Slide;

namespace JsonToMidiConverter.Test;

public record MidiNoteEvent(TimedMidiEvent On, TimedMidiEvent Off)
{

    public long Duration => Off.Time - On.Time;

    public bool IsMatching(int channel, int noteNumber)
    {
        var on = On.Event as NoteEvent;
        return on.NoteNumber == noteNumber && on.Channel == channel;
    }
}


public class SlideCase
{
    public List<InputNoteInfo> InputSourceNotes { get; set; } = [];
    public List<InputNoteInfo> InputDestinationNotes { get; set; } = [];
    public List<OutputNoteInfo> OutputNotes { get; set; } = [];
}

public class InputNoteInfo
{
    public string Id { get; set; }
    public bool IsEntryPoint { get; set; }
    public long BeatStartsAtTick { get; set; }
    public long DurationTick { get; set; }
    public int NumberOfDots { get; set; }
    public string Slide { get; set; }
    public string? TargetNoteId { get; set; }
    public int Fret { get; set; }
}

public class OutputNoteInfo
{
    public int NoteNumber { get; set; }
    public long StartsPlayingAt { get; set; }
    public long StopsPlayingAt { get; set; }
    public long PlayDuration { get; set; }
}

public record SlideRatioCase(string Slide, int Steps, bool IsIncreasing, float SlideRatio, long StepDuration, long HoldDuration, long TotalDuration, string Address);


public static class Dumper
{
    public static readonly IReadOnlyDictionary<MidiEventType, string> EventTypeNames =
        new Dictionary<MidiEventType, string>
        {
            [MidiEventType.NoteOn] = "On",
            [MidiEventType.NoteOff] = "Off",
            [MidiEventType.PitchBend] = "Pitch",
            [MidiEventType.Marker] = "Marker",
            [MidiEventType.ProgramChange] = "Program",
            [MidiEventType.ControlChange] = "Control",
        };


    public static readonly IReadOnlyDictionary<Type, JsonSerializerOptions> JsonOptions =
        new Dictionary<Type, JsonSerializerOptions>
        {

            [typeof(Measure)] = GetTypeExcludedJsonOptions(nameof(Measure.Voices)),
            [typeof(Beat)] = GetTypeExcludedJsonOptions(nameof(Beat.Notes), nameof(Beat.Text)),
            [typeof(Part)] = GetTypeExcludedJsonOptions(nameof(Part.NewLyrics), nameof(Part.Measures), nameof(Part.Automations)),
            [typeof(Voice)] = GetTypeExcludedJsonOptions(nameof(Voice.Beats)),


        };

    public static readonly HashSet<string> ExcludedProperties = new[]
    {
        "Channel", "DeltaTime", "EventType", "NoteNumber"
    }.ToHashSet();

    public static long slideAttempt1(Nota note)
    {
        // 1. Use FRET for distance (handles harmonics correctly)
        var targetFret = note.GetSlideTargetPitch();
        var fretDistance = Math.Abs(targetFret - note.Fret);

        if (note.Is("N0 B22 V0 M103 P2"))
        {

        }

        // 2. Calculate Bridge Steps
        // Distance 1 (e.g. 5->6) = 1 step
        // Distance 2 (e.g. 5->7) = 1 step
        // Distance 4 (e.g. 5->1) = 3 steps
        int bridgeSteps = (fretDistance > 1) ? fretDistance - 1 : 1;

        // 2. Determine Ratio
        // 2. Determine Logic Path based on Type
        double ratio;

        var isCollapsingHold = (note.Slide == Slide.Shift || note.Slide == Slide.Legato) && bridgeSteps > 8 &&
                               note.ActualDuration.Tick <= 3840;

        bool isShortDurationInGeneral = note.ActualDuration.Tick <= 3840;
        if (isCollapsingHold)
        {
            ratio = 1.0;
        }
        else if (note.Slide == Context.Slide.Shift)
        {
            ratio = (bridgeSteps == 1) ? 0.25 : 0.5;
        }
        else if (note.Slide == Context.Slide.Legato)
        {
            bool isUpwards = targetFret > note.Fret;
            ratio = isUpwards ? 0.5 : 0.25;
        }
        else
        {
            ratio = 0.75;
        }

        // 3. Calculate
        long maxSlideDuration = (long)(note.ActualDuration.Tick * ratio);
        long idealSlideDuration = bridgeSteps * 960;

        // 5. Decision
        if (idealSlideDuration <= maxSlideDuration)
        {
            return 960;
        }

        return maxSlideDuration / bridgeSteps;
    }

    public static long slideAttempt1Pb(Nota note)
    {
        var targetPitch = note.GetSlideTargetPitch();
        var pitchDistance = Math.Abs(targetPitch - note.Fret);

        if (note.Is("N0 B23 V0 M97 P2"))
        {

        }

        var bridgeSteps = pitchDistance > 1 ? pitchDistance - 1 : 1;
        var ratio = note.Slide == Slide.Shift
            ? 0.5
            : 0.75; // Default for Downwards/Upwards/Below

        if (note.Slide == Slide.Shift)
            ratio = 0.5; // Shift slides are capped at 50%
        else if (note.Slide == Slide.Legato)
            ratio = targetPitch > note.Fret ? 0.5 : 0.25;

        var maxSlideDuration = (long)(note.ActualDuration.Tick * ratio);

        if (bridgeSteps * 960 <= maxSlideDuration)
            return 960;

        return maxSlideDuration / bridgeSteps;
    }

    public static long slideAttempt2(Nota note)
    {
        // 1. Calculate Distances
        var targetPitch = note.GetSlideTargetPitch();
        var pitchDistance = Math.Abs(targetPitch - note.NoteNumber);
        int bridgeSteps = (pitchDistance > 1) ? pitchDistance - 1 : 1;

        // 2. Determine Ratio based on Slide Type
        // Legato and Shift are stricter (50%), while Indeterminate slides use 75%.
        double ratio = 0.75;
        if (note.Slide == Context.Slide.Legato || note.Slide == Context.Slide.Shift)
        {
            ratio = 0.50;
        }

        // 3. Calculate Durations
        long maxSlideDuration = (long)(note.ActualDuration.Tick * ratio);
        long idealSlideDuration = bridgeSteps * 960;
        long stepDuration;

        // 4. Decision
        if (idealSlideDuration <= maxSlideDuration)
        {
            return 960;
        }
        else
        {
            return maxSlideDuration / bridgeSteps;
        }
    }

    public static long slideAttempt3(Nota note)
    {
        // 1. Calculate the Target Pitch and Distance
        var targetPitch = note.GetSlideTargetPitch();
        var pitchDistance = Math.Abs(targetPitch - note.NoteNumber);

        // 2. Bridge Steps Logic
        int bridgeSteps = (pitchDistance > 1) ? pitchDistance - 1 : 1;

        // 3. Determine the Ratio (Asymmetric for Legato)
        double ratio = 0.75; // Default for Indeterminate (Up/Down/Below)

        if (note.Slide == Context.Slide.Shift)
        {
            ratio = 0.5;
        }
        else if (note.Slide == Context.Slide.Legato)
        {
            // Legato is strictly tighter when going Downwards
            bool isUpwards = targetPitch > note.NoteNumber;
            ratio = isUpwards ? 0.5 : 0.25;
        }

        // 4. Calculate Durations
        long maxSlideDuration = (long)(note.ActualDuration.Tick * ratio);
        long idealSlideDuration = bridgeSteps * 960;

        long stepDuration;

        // 5. Decision Logic
        if (idealSlideDuration <= maxSlideDuration)
        {
            return 960;
        }
        else
        {
            return maxSlideDuration / bridgeSteps;
        }

    }

    public static long slideAttempt4(Nota note)
    {
        // 1. Calculate Distance
        var targetPitch = note.GetSlideTargetPitch();
        var pitchDistance = Math.Abs(targetPitch - note.NoteNumber);
        int bridgeSteps = (pitchDistance > 1) ? pitchDistance - 1 : 1;

        // 2. Determine Ratio
        double ratio = 0.75; // Default for Upwards/Downwards/Below

        if (note.Slide == Context.Slide.Shift)
        {
            ratio = 0.5;
        }
        else if (note.Slide == Context.Slide.Legato)
        {
            // Default Legato ratio is 50%
            ratio = 0.5;

            var targetNote = note.GetSlideTargetNote();
            // EXCEPT when Legato flows into a Shift slide (Chain).
            // In that specific case, it compresses to 25%.
            if (targetNote != null && targetNote.Slide == Slide.Shift)
            {
                ratio = 0.25;
            }
        }

        // 3. Calculate Durations
        long maxSlideDuration = (long)(note.ActualDuration.Tick * ratio);
        long idealSlideDuration = bridgeSteps * 960;

        long stepDuration;

        // 4. Decision
        if (idealSlideDuration <= maxSlideDuration)
        {
            return 960;
        }
        else
        {
            return maxSlideDuration / bridgeSteps;
        }
    }

    public static void TestSlides(Song song, MidiFile midi, RecordModel record)
    {
        AssignNotesToMidiEvents(song, midi);

        var notes = song.Parts
            .SelectMany(e => e.Measures)
            .SelectMany(e => e.Voices)
            .SelectMany(e => e.Beats)
            .SelectMany(e => e.Notes);

        var multiEventNotes = notes
            .Where(e => e.MidiNoteEvents.Count > 1)
            .Where(e => e.SourceSlide == Slide.None)
            .Where(e => e.Part.InstrumentId != 1024)
            .ToList();
        Debug.Assert(multiEventNotes.Count == 0);

        var slides = notes.Where(e => e.Slide != Slide.None) // its a slide
            .Where(e => e.Index == 0) // its a leading attack note
                                      //.Where(e => e.MidiNoteEvents.Count > 1) // its not pitch bending
            .ToList();

        var results = new List<SlideCase>();
        var slideRatios = new List<SlideRatioCase>();

        foreach (var note in slides)
        {
            var slide = note.GetSlide();
            if (!slide.IsStepped) continue;

            var events = (note.TieDetails?.Source ?? note).MidiNoteEvents;
            if (events.Count == 0) continue;
            //var holdNoteEvent = events.Single(e => e.On.Event is NoteOnEvent on && on.NoteNumber == note.NoteNumber);
            var REFERNCE_steps = events.Count - 1;
            var REFERENCE_StepDuration = REFERNCE_steps == 0 ? 0 : events
                .Skip(1)
                .Average(e => e.Off.Time - e.On.Time);


            var stepDuration = slideAttempt1(note);
            var error = Math.Abs(stepDuration - REFERENCE_StepDuration);
            if (error > 5 && REFERENCE_StepDuration != 0)
            {
                var part = note.Part.Name + " - " + note.Part.Instrument;
                var id = $"{note} S{note.Song.SongId}";
                throw new Exception("fuck");
            }



            if (events.Count == 1) continue;

            var _targetPitch = note.GetSlideTargetPitch();
            var _targetNode = note.GetSlideTargetNote();

            var asd = _targetPitch > note.Fret;

            var _total = events.Sum(e => e.Duration);
            var _hold = events.First().Duration;
            var _slide = _total - _hold;
            var _steps = events.Count - 1;
            var _ratio = (double)_slide / _total;
            var _percentage = Math.Round(_ratio * 100d);
            var _stepAvg = events.Skip(1).Average(e => e.Duration);

            Cases.Add(new SlideRatioCase(
                note.Slide.ToString(),
                REFERNCE_steps,
                asd,
                (float)_ratio,
                (int)_stepAvg,
                _hold,
                _total,
                $"{note} {note.Song.SongId}"));

            var affectedNotes = new List<InputNoteInfo>();
            var affectedDestinationNotes = new List<InputNoteInfo>();
            var target = note.GetSlideTargetNote();
            if (target != null)
            {
                if (target.TieDetails != null)
                {
                    foreach (var tieNote in target.TieDetails.FullChain)
                    {
                        affectedDestinationNotes.Add(CreateInputNoteInfo(tieNote, null, false));
                    }
                }
                else affectedDestinationNotes.Add(CreateInputNoteInfo(target, null, false));
            }

            if (note.TieDetails != null)
            {
                foreach (var tieNote in note.TieDetails.FullChain)
                {
                    var isTheSource = tieNote == note;

                    affectedNotes.Add(CreateInputNoteInfo(tieNote, isTheSource ? target : null, isTheSource));
                }
            }
            else affectedNotes.Add(CreateInputNoteInfo(note, target, true));


            var sourceNote = note.Tie
                ? note.TieDetails.Source
                : note;

            results.Add(new SlideCase
            {
                InputSourceNotes = affectedNotes,
                InputDestinationNotes = affectedDestinationNotes,
                OutputNotes = sourceNote.MidiNoteEvents.Select(CreateOutputNoteInfo).ToList()
            });
        }




        File.WriteAllText($"Slides_{song.SongId}.json", JsonSerializer.Serialize(results));
    }

    public static List<SlideRatioCase> Cases = new List<SlideRatioCase>();

    public static OutputNoteInfo CreateOutputNoteInfo(MidiNoteEvent note)
    {
        var on = note.On.Event as NoteEvent;
        return new OutputNoteInfo
        {
            NoteNumber = on.NoteNumber,
            PlayDuration = note.Off.Time - note.On.Time,
            StartsPlayingAt = note.On.Time,
            StopsPlayingAt = note.Off.Time
        };
    }

    public static InputNoteInfo CreateInputNoteInfo(Nota note, Nota? targetNote, bool isTheSource)
    {
        return new InputNoteInfo()
        {
            Id = $"{note} S{note.Song.SongId}",
            BeatStartsAtTick = note.Beat.AbsoluteBeatStartTime.Tick,
            DurationTick = note.ActualDuration.Tick,
            Fret = note.Fret,
            TargetNoteId = targetNote == null ? null : $"{targetNote} S{targetNote.Song.SongId}",
            Slide = note.Slide.ToString(),
            IsEntryPoint = true
        };
    }

    public static void Dump(Song song, MidiFile midi, RecordModel record)
    {
        WriteMinifiedMidiInputJson(record);
        WriteRawMidiData(song, midi, record);
        WriteMeasuredMidiData(song, midi, record);
        ChannelSanityCheck(song, midi);
        AssignNotesToMidiEvents(song, midi);
        WriteBible(song, midi, record);
    }

    public static void WriteBible(Song song, MidiFile midi, RecordModel record)
    {
        var sb = new StringBuilder();
        foreach (var part in song.Parts)
        {
            var events = midi.GetEvents(part.Index);
            var partTitle = $"================================== P{part.Index} - I{part.InstrumentId}: {part.Instrument} {part.Name} ==================================";
            var separator = new string(Enumerable.Range(0, partTitle.Length).Select(_ => '-').ToArray());

            sb.AppendLine(separator);
            sb.AppendLine(partTitle);
            sb.AppendLine(separator);

            var writtenOnEvents = new HashSet<int>();

            foreach (var measure in part.Measures.Where(e => !e.Rest))
            {
                sb.AppendLine($"\r\n\r\n{measure}, Input = {GetJson(measure)}");
                foreach (var voice in measure.Voices)
                {
                    sb.AppendLine($"\r\n\r\n{voice}, Input = {GetJson(voice)}");
                    foreach (var beat in voice.Beats.Where(e => !e.Rest))
                    {
                        sb.AppendLine($"\r\n\t{beat}, Starts: {beat.AbsoluteBeatStartTime.Tick}, Attr = [{GetAttributes(beat)}], Input = {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {

                            var slideMarker = note.Slide != Slide.None ? $" Slide = {note.Slide} " : "";
                            var tieMarker = note.Tie ? " Tie " : "";

                            sb.AppendLine($"\t\t{note} {slideMarker}{tieMarker} CH {note.Channel}, NN {note.NoteNumber} Dur: {note.ActualDuration.Tick}, Attr = [{GetAttributes(beat)}] Input = {GetJson(note)}");

                            if (note.Is("N0 B5 V0 M20 P0"))
                            {

                            }

                            foreach (var midiNoteEvent in note.MidiNoteEvents)
                            {
                                sb.AppendLine($"\t\t\t {GetMidiEventString(midiNoteEvent.On, midiNoteEvent.Off.Time)}");
                            }

                            if (note.MidiNoteEvents.Count > 1)
                            {
                                var total = note.MidiNoteEvents.Sum(e => e.Duration);
                                var hold = note.MidiNoteEvents.OrderByDescending(e => e.Duration).First().Duration;
                                var slide = total - hold;
                                var steps = note.MidiNoteEvents.Count - 1;
                                var ratio = (double)slide / total;

                                sb.AppendLine($"\t\t\t ----- Total: {total}, Hold: {hold}, Slide: {slide}, Steps: {steps}, Slide Ratio: {ratio:P2}");
                            }

                        }
                    }
                }
            }

            var missingOns = events.Where(e => e.Event is NoteOnEvent && !writtenOnEvents.Contains(e.Index)).ToList();

            //Debug.Assert(missingOns.Count == 0);
        }
        File.WriteAllText($"{record.Title}_Bible", sb.ToString());
    }

    private static bool IsMatchingNoteEvent(MidiEvent midiEvent, Nota note)
        => midiEvent is NoteEvent noteEvent && noteEvent.Channel == note.Channel && noteEvent.NoteNumber == note.NoteNumber;

    public static bool ShouldISkipThisBecauseTheyFuckedUpTheirMidi(Nota note)
    {
        if (note.Song.SongId == 14) // come as you are
        {
            if (note.Is("N1 B3 V0 M49 P6")) return true; // a random ghost kick which is not processed
            if (note.Measure.Is("M3 P6")) return true; // the whole fucking measure is missing
        }

        return false;
    }

    private static void ChannelSanityCheck(Song song, MidiFile midi)
    {

        foreach (var part in song.Parts)
        {
            var chunk = midi.GetEvents(part.Index);

            var midiInstrumentId = chunk.First(e => e.Event.Is<ProgramChangeEvent>()).Event.As<ProgramChangeEvent>().ProgramNumber;
            var midiInstrumentName = chunk.FirstOrDefault(e => e.Event.Is<InstrumentNameEvent>())?.Event.As<InstrumentNameEvent>().Text;
            var midiTrackName = chunk.FirstOrDefault(e => e.Event.Is<SequenceTrackNameEvent>())?.Event.As<SequenceTrackNameEvent>().Text;

            Debug.Assert(part.Measures.Count == chunk.Count(e => e.Event is MarkerEvent marker && marker.Text.StartsWith("MEASURE")));
            Debug.Assert(part.InstrumentId.To7() == midiInstrumentId);
            Debug.Assert(midiInstrumentName == null || part.Instrument == midiInstrumentName);
            Debug.Assert(midiTrackName == null || part.Name == midiTrackName);

            var firstBeat = part.Measures
                    .SelectMany(e => e.Voices)
                    .SelectMany(e => e.Beats)
                    .First(e => e.Notes.Any(w => !w.Tie && !w.Rest));

            var firstNotes = firstBeat.Notes
                .Where(e => !e.Tie && !e.Rest)
                .OrderBy(e => e.Index)
                .ToList();

            var firstEvent = chunk.GetMeasureEvents(firstNotes[0].Measure)
                .FirstOrDefault(e => e.Event.EventType == MidiEventType.NoteOn);

            if (firstEvent == null) continue;

            var match = false;
            foreach (var note in firstNotes)
            {
                match |= IsMatchingNoteEvent(firstEvent.Event, note);
            }

            Debug.Assert(match);

            var ch = firstNotes[0].GetNoteChannel();
            var nn = firstNotes[0].GetNoteNumber();

            break;
        }
    }

    private static IEnumerable<MidiNoteEvent> GatherNoteOnOffParis(List<TimedMidiEvent> events)
    {
        var noteOns = events.Where(e => e.Event.EventType == MidiEventType.NoteOn).ToList();
        var noteOffs = events.Where(e => e.Event.EventType == MidiEventType.NoteOff).ToList();

        Debug.Assert(noteOffs.Count == noteOns.Count);

        foreach (var noteOn in noteOns)
        {
            var on = noteOn.Event as NoteOnEvent;
            var noteOff = noteOffs.First(e =>
            {
                var off = e.Event as NoteOffEvent;
                return on.Channel == off.Channel && on.NoteNumber == off.NoteNumber;
            });

            noteOffs.Remove(noteOff);

            yield return new(noteOn, noteOff);
        }
    }

    public static void AssignNotesToMidiEvents(Song song, MidiFile midi)
    {
        foreach (var part in song.Parts)
        {
            var allEvents = midi.GetEvents(part.Index).ToList();
            var assertEvents = allEvents.ToList();
            var events = GatherNoteOnOffParis(allEvents).ToList();

            var usedIndexes = new HashSet<int>();

            var notes = part.Measures
                .SelectMany(e => e.Voices)
                .SelectMany(e => e.Beats)
                .SelectMany(n => n.Notes)
                .Where(e => !e.Rest && !e.Tie && !ShouldISkipThisBecauseTheyFuckedUpTheirMidi(e))
                .OrderBy(e => e.GetStartTime().Tick)
                .ThenBy(e => e.Index)
                .ToList();

            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];

                var ch = note.GetNoteChannel();
                var nn = note.GetNoteNumber();



                var noteEvent = events
                    .SkipWhile(e => !IsMatchingNoteEvent(e.On.Event, note))
                    .First();



                var measureFound = false;
                for (var m = noteEvent.On.Index; m > -1; m--)
                {
                    if (allEvents[m].Event is MarkerEvent marker)
                    {
                        Debug.Assert(marker.Text == $"MEASURE_{note.Measure.Index}");
                        measureFound = true;
                        break;

                    }
                }
                Debug.Assert(measureFound);

                if (note.Is("N0 B0 V0 M119 P6"))
                {

                }

                note.MidiNoteEvents.Add(noteEvent);

                var nextChannelNote = notes
                    .Skip(i + 1)
                    .SkipWhile(e => !(e.Part.InstrumentId == 1024
                ? e.NoteNumber == note.NoteNumber
                : e.StringNumber == note.StringNumber))
                    // TODO: maybe this should be ch comparis
                    // : e.StringNumber == note.StringNumber))
                    .FirstOrDefault();

                var nextChannelNoteEvent = nextChannelNote != null
                    ? events.SkipWhile(e => !(IsMatchingNoteEvent(e.On.Event, nextChannelNote) &&
                                              e.On.Time >= nextChannelNote.Beat.AbsoluteBeatStartTime.Tick)).First()
                    : null;

                var nextChannelNoteEventIndex = nextChannelNoteEvent?.On.Index ?? int.MaxValue;

                var inBetweenNotes = events
                    .SkipWhile(e => e.On.Index <= noteEvent.On.Index)
                    .TakeWhile(e => e.On.Index < nextChannelNoteEventIndex)
                    .Where(e => e.On.Event is NoteOnEvent on &&
                                (
                                    on.Channel == 9
                                        ? on.NoteNumber == nn
                                        : on.Channel == ch))
                    .ToList();

                note.MidiNoteEvents.AddRange(inBetweenNotes);

                foreach (var assignedEvent in note.MidiNoteEvents)
                {
                    events.Remove(assignedEvent);
                }

            }


            Debug.Assert(events.Count == 0);


        }
    }

    private static void WriteMinifiedMidiInputJson(RecordModel record)
    {
        var song = Database.GetMidiData(record.SongId);

        var sb = new StringBuilder();
        foreach (var part in song.Parts)
        {
            sb.AppendLine($"\t P{part.Index} {part.Name} {part.Instrument} I{part.InstrumentId}, Tempo changes:");
            foreach (var tempoChange in part.Automations.Tempo)
            {
                sb.AppendLine($"\t\t - {GetJson(tempoChange)}]");
            }
        }

        sb.AppendLine();
        sb.AppendLine();

        var p = 0;
        foreach (var part in song.Parts)
        {
            var m = 0;
            sb.AppendLine($"\r\n\r\nP{p} {GetJson(part)}");
            foreach (var measure in part.Measures)
            {
                var v = 0;
                sb.AppendLine($"\r\n\tM{m} P{p} {GetJson(measure)}");
                foreach (var voice in measure.Voices)
                {
                    var b = 0;
                    sb.AppendLine($"\r\n\t\tV{v} M{m} P{p} {GetJson(voice)}");
                    foreach (var beat in voice.Beats)
                    {
                        var n = 0;
                        sb.AppendLine($"\t\t\tB{b} V{v} M{m} P{p} Starts: {beat.AbsoluteBeatStartTime.Tick} {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {

                            sb.AppendLine($"\t\t\t\tN{n} B{b} V{v} M{m} P{p} Dur: {note.ActualDuration.Tick} {GetJson(note)}");
                            n++;
                        }

                        b++;
                    }

                    v++;
                }

                m++;
            }

            p++;
        }

        File.WriteAllText($"{record.Title}_JsonMini.json", sb.ToString());
    }

    private static void WriteMeasuredMidiData(Song song, MidiFile midi, RecordModel record)
    {
        var sb = new StringBuilder();

        foreach (var part in song.Parts)
        {
            var partEvents = midi.GetEvents(part.Index);

            sb.AppendLine($"\r\n\r\n{part} {GetJson(part)}");
            foreach (var measure in part.Measures)
            {
                sb.AppendLine($"\r\n\t{measure} {GetJson(measure)}");
                foreach (var voice in measure.Voices)
                {
                    sb.AppendLine($"\r\n\t\t{voice} {GetJson(voice)}");
                    foreach (var beat in voice.Beats)
                    {
                        sb.AppendLine($"\t\t\t{beat} {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {
                            sb.AppendLine(
                                $"\t\t\t\t{note} {GetJson(note)}");
                        }
                    }
                }

                if (measure.Index == 9)
                {

                }

                var w = part.Measures.Count;
                var markerEvents = partEvents.Where(e => e.Event.EventType == MidiEventType.Marker).ToList();

                var asd = song.Parts.All(e => e.Measures.Count == song.Parts[0].Measures.Count);
                var firstChunk = midi.Chunks.OfType<TrackChunk>().First().Events
                    .Count(e => e.EventType == MidiEventType.Marker);
                var qwe = midi.Chunks.OfType<TrackChunk>().All(e =>
                    e.Events.Count(w => w.EventType == MidiEventType.Marker) == firstChunk);

                var events = partEvents.GetMeasureEvents(measure);
                var q = partEvents.Where(e => e.Event.EventType == MidiEventType.Marker).ToList();

                var time = 0L;
                foreach (var timedEvent in events.Where(e => e.Event.EventType != MidiEventType.PitchBend))
                {
                    sb.AppendLine($"\t\t {GetMidiEventString(timedEvent)}");
                }
            }
        }

        File.WriteAllText($"{record.Title}_MidiMeasured.dani", sb.ToString());
    }

    private static void WriteRawMidiData(Song song, MidiFile midi, RecordModel record)
    {
        var sb = new StringBuilder();

        foreach (var part in song.Parts)
        {
            var partEvents = midi.GetEvents(part.Index);
            foreach (var ev in partEvents)
            {
                sb.AppendLine($"\t\t {GetMidiEventString(ev)}");
            }
        }

        File.WriteAllText($"{record.Title}_MidiRaw.dani", sb.ToString());
    }

    private static string GetMidiEventString(TimedMidiEvent timedEvent, long? end = null)
    {
        EventTypeNames.TryGetValue(timedEvent.Event.EventType, out var niceName);

        var ch = (timedEvent.Event as ChannelEvent)?.Channel.ToString() ?? string.Empty;
        var nn = (timedEvent.Event as NoteEvent)?.NoteNumber.ToString() ?? string.Empty;

        var properties = timedEvent.Event
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(e => !ExcludedProperties.Contains(e.Name));

        var attributes = string.Join("; ", properties
            .OrderBy(e => e.Name)
            .Select(prop => $"{prop.Name}: {prop.GetValue(timedEvent.Event)}"));

        var timing = $"Start: {timedEvent.Time}";
        if (end.HasValue)
        {
            var duration = end.Value - timedEvent.Time;
            timing += $", End: {end.Value}, Duration: {duration,5}";
        }

        return
            $"{timedEvent.Index.ToString(),5} {(niceName ?? timedEvent.Event.EventType.ToString()),-5} " +
            $"Note: {nn,2}; {timing}; Ch: {ch}; " +
            $"{attributes}";
    }

    public static string GetAttributes(object model)
    {
        return model switch
        {
            Nota note => string.Join(", ", note.Rest ? "Rest" : "", note.Tie ? "Tie" : ""),
            Beat beat => string.Join(", ", beat.Rest ? "Rest" : ""),

            _ => string.Empty
        };
    }

    public static string GetJson<T>(T model)
    {
        if (!JsonOptions.TryGetValue(typeof(T), out var options))
            options = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };

        return JsonSerializer.Serialize(model, options);
    }

    private static JsonSerializerOptions GetTypeExcludedJsonOptions(params string[] excludedProperties)
    {
        var hash = excludedProperties.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo => typeInfo.Properties
                        .Where(e => hash.Contains(e.Name))
                        .ToList()
                        .ForEach(prop => typeInfo.Properties.Remove(prop))
                }
            }
        };
    }

}