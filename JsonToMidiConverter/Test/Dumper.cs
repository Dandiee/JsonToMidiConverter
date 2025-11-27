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

                            sb.AppendLine($"\t\t{note} {slideMarker}{tieMarker} CH{note.Channel} NN{note.NoteNumber} Attr = [{GetAttributes(beat)}] Input = {GetJson(note)}");

                            if (note.MidiStartEventIndex.HasValue)
                            {
                                for (var i = note.MidiStartEventIndex.Value; i < note.MidiEndEventIndex.Value; i++)
                                {
                                    var timedEvent = events[i];
                                    sb.AppendLine($"\t\t\t {GetMidiEventString(timedEvent)}");
                                }
                            }
                        }
                    }
                }
            }
        }
        File.WriteAllText($"{record.Title}_Bible", sb.ToString());
    }

    private static bool IsMatchingNoteOn(MidiEvent midiEvent, Nóta note) => midiEvent is NoteOnEvent on && on.Channel == note.Channel && on.NoteNumber == note.NoteNumber;

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
                match |= IsMatchingNoteOn(firstEvent.Event, note);
            }

            Debug.Assert(match);

            var ch = firstNotes[0].GetNoteChannel();
            var nn = firstNotes[0].GetNoteNumber();

            break;
        }
    }

    public static void AssignNotesToMidiEvents(Song song, MidiFile midi)
    {
        foreach (var part in song.Parts)
        {
            var events = midi.GetEvents(part.Index);

            foreach (var measure in part.Measures.Where(e => !KnownFuckedUpMeasures.Contains(e.ToString())))
            {
                var measureEvents = events.GetMeasureEvents(measure);

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

                    var ch = note.GetNoteChannel();
                    var nn = note.GetNoteNumber();

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

    private static void WriteMinifiedMidiInputJson(RecordModel record)
    {
        var song = Database.GetMidiData(record.SongId);
        var meta = Database.GetMetaData(record.SongId);

        //File.WriteAllText($"{record.Title}_Meta.json", meta);

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
                foreach (var timedEvent in events)
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

    private static string GetMidiEventString(TimedMidiEvent timedEvent)
    {
        EventTypeNames.TryGetValue(timedEvent.Event.EventType, out var niceName);

        var ch = (timedEvent.Event as ChannelEvent)?.Channel.ToString() ?? string.Empty;
        var nn = (timedEvent.Event as NoteEvent)?.NoteNumber.ToString() ?? string.Empty;

        var properties = timedEvent
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(e => !ExcludedProperties.Contains(e.Name));

        var attributes = string.Join("; ", properties
            .OrderBy(e => e.Name)
            .Select(prop => $"{prop.Name}: {prop.GetValue(timedEvent)}"));

        return
            $"{timedEvent.Index.ToString(),5} {(niceName ?? timedEvent.Event.EventType.ToString()),-10} " +
            $"Note: {nn,2}; At: {timedEvent.Time}; Ch: {ch}; " +
            $"Delta: {timedEvent.Event.DeltaTime.ToString(),6} {attributes}";
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