using JsonToMidiConverter.Models;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Slide = JsonToMidiConverter.Context.Slide;

namespace JsonToMidiConverter.Test;

public record MidiNoteEvent(TimedMidiEvent On, TimedMidiEvent Off);

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

    public static readonly HashSet<string> KnownFuckedUpMeasures = new[] { "M3 P6", "M17 P1" }.ToHashSet();

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
                        sb.AppendLine($"\r\n\t{beat}, Attr = [{GetAttributes(beat)}], Input = {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {

                            var slideMarker = note.Slide != Slide.None ? $" Slide = {note.Slide} " : "";
                            var tieMarker = note.Tie ? " Tie " : "";

                            sb.AppendLine($"\t\t{note} {slideMarker}{tieMarker} CH {note.Channel}, NN {note.NoteNumber} Attr = [{GetAttributes(beat)}] Input = {GetJson(note)}");

                            if (note.Is("N0 B5 V0 M20 P0"))
                            {

                            }

                            foreach (var midiNoteEvent in note.MidiNoteEvents)
                            {
                                sb.AppendLine($"\t\t\t {GetMidiEventString(midiNoteEvent.On, midiNoteEvent.Off.Time)}");
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

    private static bool IsMatchingNoteEvent(MidiEvent midiEvent, Nóta note)
        => midiEvent is NoteEvent noteEvent && noteEvent.Channel == note.Channel && noteEvent.NoteNumber == note.NoteNumber;

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
                                              e.On.Time >= nextChannelNote.Beat.AbsoluteBeatStartTime.Tick )).First()
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
                        sb.AppendLine($"\t\t\tB{b} V{v} M{m} P{p} {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {

                            sb.AppendLine($"\t\t\t\tN{n} B{b} V{v} M{m} P{p} {GetJson(note)}");
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