using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Slide = JsonToMidiConverter.Context.Slide;

namespace JsonToMidiConverter.Test;

public record Event(MidiEvent MidiEvent, long Time);

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

    public static readonly HashSet<string> KnownFuckedUpMeasures = new[] { "M3 P6" }.ToHashSet();

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

    public static readonly HashSet<MidiEventType> StrumSeparatorEvents = new[]
    {
        MidiEventType.NoteOff, MidiEventType.ControlChange, MidiEventType.PitchBend
    }.ToHashSet();

    public static bool IsMissing(Nóta note)
    {
        if (note.Part.InstrumentId == 1024 && note.Fret == 36 && note.Ghost) return true;

        return false;
    }

    public static void Dump(Song song, string midiPath, string? artist, string title)
    {
        var midi = MidiFile.Read(midiPath);

        ChannelSanityCheck(song, midi);

        var output = ProcessMidi(song, midi);

        File.WriteAllText($"{title}_Bible", output);
    }

    private static IEnumerable<Event> GetMidiEvents(TrackChunk chunk)
    {
        var absoluteTime = 0L;

        foreach (var midiEvent in chunk.Events)
        {
            absoluteTime += midiEvent.DeltaTime;
            //var time = ((absoluteTime + 9) / 10) * 10;
            var time = absoluteTime;
            yield return new(midiEvent, time);
        }
    }

    public static long R(this long n) => (long)Math.Round(n / 10d) * 10;

    public static string GetNoteDetails(Nóta note) =>
        $"{note.GetName()}; Duration = {note.ActualDuration.ToString().PadLeft(12)} JSON = {GetJson(note.Beat)} {GetJson(note)}";

    public record SlideInfo(int Steps, Time HoldDuration, Time SlideWindow, Time SlideNoteDuration);

    public static void TestTheory(Nóta attackNote,
        int testNumberOfSteps,
        long testTotalDuration,
        long testSlideWindow,
        long testSlideNoteDelay)
    {

        if (attackNote.Index > 0)
        {
            return;
        }

        var instrument = attackNote.Part.Instrument;
        var name = attackNote.Part.Name;

        var attackNoteNumber = attackNote.NoteNumber;
        var landingNoteNumber = attackNote.GetSlideTargetPitch();

        var steps = Math.Abs(landingNoteNumber - attackNoteNumber) - 1;
        if (steps < 2) return;
        var duration = attackNote?.TieDetails?.Destination.ActualDuration.Tick ?? attackNote.ActualDuration.Tick;



        var slideWindow = attackNote.Slide == Slide.Downwards || attackNote.Slide == Slide.Upwards
            ? 0.75 * duration
            : Math.Min(steps * 960d, duration / 2d);
        var holdDuration = duration - slideWindow;

        var vibratoMultiplier = attackNote.Vibrato ? 1.33333 : 1.0;
        var dotMultiplier = (2 - (1 / Math.Pow(2, attackNote.Beat.Dots)));
        var stepSize = Math.Min(960, slideWindow / steps) * dotMultiplier * vibratoMultiplier;

        var info = attackNote.GetSlide();

        //Debug.Assert(testNumberOfSteps == info.Steps);
        //Debug.Assert(Math.Abs(info.StepDuration.Tick - testSlideNoteDelay) < 10);
        //
        //Debug.Assert(testNumberOfSteps == steps);
        //Debug.Assert(Math.Abs(stepSize - testSlideNoteDelay) < 10);

    }

    public static string ProcessMidi(Song song, MidiFile midi)
    {
        var sb = new StringBuilder();

        AssignNotesToMidiEvents(song, midi);

        var chunks = midi.Chunks.OfType<TrackChunk>().ToList();

        Debug.Assert(song.Parts.Length == chunks.Count);



        foreach (var part in song.Parts)
        {
            var events = GetMidiEvents(chunks[part.Index]).ToList();

            var instrumentId = ((ProgramChangeEvent)events[0].MidiEvent).ProgramNumber;
            var trackName = ((SequenceTrackNameEvent)events.Single(e => e.MidiEvent.EventType == MidiEventType.SequenceTrackName).MidiEvent).Text;
            var instrumentName = ((InstrumentNameEvent)events.Single(e => e.MidiEvent.EventType == MidiEventType.InstrumentName).MidiEvent).Text;


            Debug.Assert(part.Instrument == instrumentName);
            Debug.Assert(part.Name == trackName);
            //Debug.Assert(part.InstrumentId == instrumentId);
            Debug.Assert(part.Measures.Count == events.Count(e => e.MidiEvent.EventType == MidiEventType.Marker) - 1);

            var partTitle =
                $"================================== P{part.Index} - I{part.InstrumentId}: {part.Instrument} {part.Name} ==================================";
            var separator = new string(Enumerable.Range(0, partTitle.Length).Select(_ => '-').ToArray());
            sb.AppendLine(separator);
            sb.AppendLine(partTitle);
            sb.AppendLine(separator);


            foreach (var measure in part.Measures)
            {
                if (measure.Rest) continue;


                sb.AppendLine($"\r\n\r\n{measure}, Input = {GetJson(measure)}");
                foreach (var voice in measure.Voices)
                {

                    sb.AppendLine($"\r\n\r\n{voice}, Input = {GetJson(voice)}");
                    foreach (var beat in voice.Beats)
                    {
                        if (beat.Rest) continue;

                        sb.AppendLine($"\r\n\t{beat}, Attr = [{GetAttributes(beat)}], Input = {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {

                            var slideMarker = note.Slide != Slide.None ? $" Slide = {note.Slide} " : "";
                            var tieMarker = note.Tie ? " Tie " : "";

                            sb.AppendLine($"\t\t{note} {slideMarker}{tieMarker} CH{note.GetNoteChannel()} NN{note.GetNoteNumber()} Attr = [{GetAttributes(beat)}] Input = {GetJson(note)}");

                            if (note.MidiStartEventIndex.HasValue)
                            {
                                for (var i = note.MidiStartEventIndex.Value; i < note.MidiEndEventIndex.Value; i++)
                                {
                                    var timedEvent = events[i];
                                    EventTypeNames.TryGetValue(timedEvent.MidiEvent.EventType, out var niceName);

                                    var ch = (timedEvent.MidiEvent as ChannelEvent)?.Channel.ToString() ?? string.Empty;
                                    var nn = (timedEvent.MidiEvent as NoteEvent)?.NoteNumber.ToString() ?? string.Empty;

                                    var properties = timedEvent.MidiEvent
                                        .GetType()
                                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(e => !ExcludedProperties.Contains(e.Name));

                                    var attributes = string.Join("; ", properties
                                        .OrderBy(e => e.Name)
                                        .Select(prop => $"{prop.Name}: {prop.GetValue(timedEvent.MidiEvent)}"));

                                    sb.AppendLine(
                                        $"\t\t\t{i.ToString().PadLeft(5)} {(niceName ?? timedEvent.MidiEvent.EventType.ToString()).PadRight(10)} " +
                                        $"Note: {nn.PadLeft(2)}; At: {timedEvent.Time}; Ch: {ch}; " +
                                        $"Delta: {timedEvent.MidiEvent.DeltaTime.ToString().PadLeft(6)} {attributes}");
                                }

                            }
                        }
                    }

                }
            }
        }


        return sb.ToString();
    }

    public static string GetAttributes(object model)
    {
        return model switch
        {
            Nóta note => string.Join(", ", note.Rest ? "Rest" : "", note.Tie ? "Tie" : ""),
            Beat beat => string.Join(", ", beat.Rest ? "Rest" : ""),

            _ => string.Empty
        };
    }

    public static string GetJson<T>(T model)
    {
        if (!JsonOptions.TryGetValue(typeof(T), out var options))
            options = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            };

        return JsonSerializer.Serialize(model, options);
    }

    private static bool IsPitchClearing(List<MidiEvent> events, int cursor, Nóta note)
    {
        var midiEvent = events[cursor];
        if (midiEvent is not PitchBendEvent pitch) return false;
        if (pitch.PitchValue != 8192) return false;
        if (pitch.Channel != note.Channel) return false;

        var q = note.GetNoteChannel();

        return true;
    }

    private static bool IsMatchingNote(List<MidiEvent> events, int cursor, Nóta note)
    {
        var midiEvent = events[cursor];
        if (midiEvent is not NoteOnEvent on) return false;
        if (on.DeltaTime != 0 || on.NoteNumber != note.NoteNumber) return false;

        var w = note.GetNoteNumber();

        return true;
    }

    private static bool IsMatchingNoteOn(MidiEvent midiEvent, Nóta note)
        => midiEvent is NoteOnEvent on && on.Channel == note.Channel && on.NoteNumber == note.NoteNumber;

    private static bool IsMatchingMeasureMarker(MidiEvent midiEvent, Measure measure)
        => midiEvent is MarkerEvent marker && marker.Text == $"MEASURE_{measure.Index}";

    public static bool ShouldISkipThisBecauseTheyFuckedUpTheirMidi(Nóta note)
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

            Debug.Assert(part.InstrumentId.To7() == midiInstrumentId);
            Debug.Assert(midiInstrumentName == null || part.Instrument == midiInstrumentName);
            Debug.Assert(midiTrackName == null || part.Name == midiTrackName);

            var firstBeat = part.Measures
                .Where(e => !KnownFuckedUpMeasures.Contains(e.ToString()))
                .SelectMany(e => e.Voices)
                .SelectMany(e => e.Beats)
                .First(e => e.Notes.Any(w => !w.Tie && !w.Rest));

            var firstNotes = firstBeat.Notes
                .Where(e => !e.Tie && !e.Rest)
                .OrderBy(e => e.Index)
                .ToList();

            // 860160

            var firstEvent = midi
                .GetEvents(part.Index)
                .First(e => e.Event.EventType == MidiEventType.NoteOn);

            var match = false;
            foreach (var note in firstNotes)
            {
                match |= IsMatchingNoteOn(firstEvent.Event, note);
            }

            Debug.Assert(match);

            var ch = firstNotes[0].GetNoteChannel();
            var nn = firstNotes[0].GetNoteNumber();

        }
        

    }

    public static void AssignNotesToMidiEvents(Song song, MidiFile midi)
    {
        foreach (var part in song.Parts)
        {
            var events = midi.GetEvents(part.Index);

            foreach (var measure in part.Measures.Where(e => !KnownFuckedUpMeasures.Contains(e.ToString())))
            {
                var measureEvents = events
                    .SkipWhile(e => !IsMatchingMeasureMarker(e.Event, measure))
                    .TakeWhile(e => IsMatchingMeasureMarker(e.Event, measure) || e.Event is not MarkerEvent)
                    .ToList();

                var noteOnEvents = measureEvents
                    .Where(e => e.Event.EventType == MidiEventType.NoteOn)
                    .ToList();

                var notes = measure.Voices
                    .SelectMany(e => e.Beats)
                    .SelectMany(n => n.Notes)
                    .Where(e => !e.Rest && !e.Tie && !ShouldISkipThisBecauseTheyFuckedUpTheirMidi(e))
                    .OrderBy(e => e.GetStartTime().Tick)
                    .ToList();

                for (var i = 0; i < notes.Count; i++)
                {
                    

                    var note = notes[i];
                    var nextNote = i == notes.Count - 1 ? null : notes[i + 1];

                    if (note.Is("N1 B0 V0 M0 P6"))
                    {

                    }

                    var noteOnEvent = noteOnEvents
                        .SkipWhile(e => !IsMatchingNoteOn(e.Event, note))
                        .First();

                    noteOnEvents.Remove(noteOnEvent);

                    var closingEvent = i == notes.Count - 1
                        ? measureEvents[^1]
                        : noteOnEvents
                            .SkipWhile(e => !IsMatchingNoteOn(e.Event, nextNote))
                            .First();

                    note.MidiStartEventIndex = i == 0 ? measureEvents[0].Index : noteOnEvent.Index;
                    note.MidiEndEventIndex = closingEvent.Index + (closingEvent == noteOnEvent ? 1 : 0);
                }
            }
        }
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

    public static void DumpJsonInputs(int songId, string? midiFilePath = null)
    {
        var originalTimeMap = Time.Map;

        var meta = Database.GetMetaData(songId);
        var song = Database.GetMidiData(songId);

        var opts = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        File.WriteAllText($"{meta.Title}_MidiData.json", JsonSerializer.Serialize(meta, opts));
        File.WriteAllText($"{meta.Title}_JsonData.json", JsonSerializer.Serialize(song, opts));


        var dumpMid = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(15360) };
        Time.Map = song.Parts[0].GetTempo(dumpMid);
        song.Build(dumpMid);
        Time.Map = originalTimeMap;


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

        foreach (var part in song.Parts)
        {
            sb.AppendLine($"\r\n\r\n{part} {GetJson(part)}");
            foreach (var measure in part.Measures)
            {

                sb.AppendLine($"\r\n\t{measure} {GetJson(measure)}");
                foreach (var voice in measure.Voices)
                {
                    sb.AppendLine($"\r\n\t\t{voice} {GetJson(voice)}");
                    foreach (var beat in voice.Beats)
                    {
                        sb.AppendLine($"\t\t\tB{beat} {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {
                            sb.AppendLine(
                                $"\t\t\t\t{note} {GetJson(note)}");
                        }
                    }
                }
            }
        }

        File.WriteAllText($"{meta.Title}_JsonRaw.json", sb.ToString());









        sb = new StringBuilder();


        var midi = MidiFile.Read(midiFilePath);
        var chunks = midi.Chunks.OfType<TrackChunk>().ToList();

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

                var events = partEvents
                    .SkipWhile(e => !(e.Event is MarkerEvent marker && marker.Text == $"MEASURE_{measure.Index}"))
                    .TakeWhile(e => e.Event is not MarkerEvent marker || marker.Text == $"MEASURE_{measure.Index}")
                    .ToList();

                var time = 0L;
                foreach (var evnt in events)
                {
                    var midiEvent = evnt.Event;
                    time += midiEvent.DeltaTime;

                    EventTypeNames.TryGetValue(midiEvent.EventType, out var niceName);

                    var ch = (midiEvent as ChannelEvent)?.Channel.ToString() ?? string.Empty;
                    var nn = (midiEvent as NoteEvent)?.NoteNumber.ToString() ?? string.Empty;

                    var properties = midiEvent
                        .GetType()
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(e => !ExcludedProperties.Contains(e.Name));

                    var attributes = string.Join("; ", properties
                        .OrderBy(e => e.Name)
                        .Select(prop => $"{prop.Name}: {prop.GetValue(midiEvent)}"));

                    sb.AppendLine(
                        $"\t\t{evnt.Index.ToString().PadLeft(5)} {(niceName ?? midiEvent.EventType.ToString()).PadRight(10)} " +
                        $"Note: {nn.PadLeft(2)}; At: {time}; Ch: {ch}; " +
                        $"Delta: {midiEvent.DeltaTime.ToString().PadLeft(6)} {attributes}");
                }
            }
        }


        File.WriteAllText($"{meta.Title}_MidiRaw.dani", sb.ToString());

    }
}