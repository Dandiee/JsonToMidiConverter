using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JsonToMidiConverter.Context;

namespace JsonToMidiConverter.Test;

public static class Dumper
{
    public static readonly IReadOnlyDictionary<MidiEventType, string> EventTypeNames = new Dictionary<MidiEventType, string>
    {
        [MidiEventType.NoteOn] = "On",
        [MidiEventType.NoteOff] = "Off",
        [MidiEventType.PitchBend] = "Pitch",
        [MidiEventType.Marker] = "Marker",
        [MidiEventType.ProgramChange] = "Program",
    };

    public static readonly IReadOnlyDictionary<Type, JsonSerializerOptions> JsonOptions = new Dictionary<Type, JsonSerializerOptions>
    {
        [typeof(Measure)] = GetTypeExcludedJsonOptions(nameof(Measure.Voices)),
        [typeof(Beat)] = GetTypeExcludedJsonOptions(nameof(Beat.Notes), nameof(Beat.Text))
    };

    public static readonly HashSet<string> ExcludedProperties = new[] { "Channel", "DeltaTime", "EventType", "NoteNumber" }.ToHashSet();

    public static void Dump(Song song, string midiPath)
    {
        var midi = MidiFile.Read(midiPath);
        var output = ProcessMidi(song, midi);
        File.WriteAllText("Logs.txt", output);
    }

    private static IEnumerable<(long Time, MidiEvent Event)> GetMidiEvents(TrackChunk chunk)
    {
        var absoluteTime = 0L;

        foreach (var midiEvent in chunk.Events)
        {
            absoluteTime += midiEvent.DeltaTime;
            yield return new(absoluteTime, midiEvent);
        }
    }

    public static string ProcessMidi(Song song, MidiFile midi)
    {
        var sb = new StringBuilder();

        SetSongMidiEvents(song, midi);

        var chunks = midi.Chunks.OfType<TrackChunk>().ToList();

        foreach (var part in song.Parts)
        {
            var events = GetMidiEvents(chunks[part.Index]).ToList();

            foreach (var measure in part.Measures)
            {
                sb.AppendLine($"\r\n\r\nM{measure.Index} P{part.Index}, Input = {GetJson(measure)}");

                foreach (var beat in measure.Beats)
                {
                    sb.AppendLine($"\r\n\tB{beat.Index} M{measure.Index} P{part.Index}, Attr = [{GetAttributes(beat)}], Input = {GetJson(beat)}");
                    foreach (var note in beat.Notes)
                    {

                        var slideMarker = note.Slide != Slide.None ? $" Slide = {note.Slide} " : "";
                        var tieMarker = note.Tie ? " Tie " : "";

                        sb.AppendLine($"\t\tN{note.Index} B{beat.Index} M{measure.Index} P{part.Index} S{note.StringNumber} F{note.Fret}" + 
                                      $"{slideMarker}{tieMarker} Attr = [{GetAttributes(beat)}] Input = {GetJson(note)}");

                        if (note.MidiEventIndex.HasValue)
                        {
                            var from = note.MidiEventIndex.Value;
                            var to = note.MidiEventCount.HasValue
                                ? note.MidiEventCount.Value + from
                                : events.Count;

                            var pitchCounter = 0;

                            for (var i = from; i < to; i++)
                            {
                                var timedEvent = events[i];
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

                                if (timedEvent.Event is PitchBendEvent)
                                {
                                    pitchCounter++;
                                }
                                else pitchCounter = 0;

                                if (pitchCounter == 3)
                                {
                                    sb.AppendLine($"\t\t\t.... More pitch bending" );
                                }
                                else if(pitchCounter < 3)
                                {
                                    sb.AppendLine(
                                        $"\t\t\t{i.ToString().PadLeft(5)} {(niceName ?? timedEvent.Event.EventType.ToString()).PadRight(10)} " +
                                        $"Note: {nn.PadLeft(2)}; At: {timedEvent.Time}; Ch: {ch}; " + 
                                        $"Delta: {timedEvent.Event.DeltaTime.ToString().PadLeft(6)} {attributes}");
                                }
                            }

                        }
                    }
                }
            }
        }

        return sb.ToString();
    }

    public static void SetSongMidiEvents(Song song, MidiFile midi)
    {
        var chunks = midi.Chunks.OfType<TrackChunk>().ToList();

        foreach (var part in song.Parts)
        {
            var chunk = chunks[part.Index];
            var events = chunk.Events.ToList();

            Nóta? lastMarkedNote = null;
            var cursor = 0;

            foreach (var measure in part.Measures)
            {
                foreach (var beat in measure.Beats)
                {
                    foreach (var note in beat.Notes)
                    {
                        if (!beat.Rest && !note.Rest && !note.Tie)
                        {
                            cursor = GetNextAttackNoteEvent(events, cursor, note);
                            note.MidiEventIndex = cursor;

                            if (lastMarkedNote != null)
                            {
                                lastMarkedNote.MidiEventCount = cursor - lastMarkedNote.MidiEventIndex!.Value;
                            }

                            lastMarkedNote = note;
                            break;
                        }
                    }
                }
            }
        }
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

    public static int GetNextAttackNoteEvent(List<MidiEvent> events, int cursor, Nóta note)
    {
        while (true)
        {
            cursor++;
            var cursorEvent = events[cursor];
            if (cursorEvent is PitchBendEvent pitch && pitch.PitchValue == 8192)
            {
                var nextEvent = events[cursor + 1];
                if (nextEvent is NoteOnEvent on
                    && on.DeltaTime == 0
                    && on.Channel == note.Channel
                    && on.NoteNumber == note.NoteNumber)
                {

                    var measureCursor = cursor;
                    for (var i = measureCursor; ; i--)
                    {
                        var ev = events[i];
                        if (ev is MarkerEvent marker)
                        {
                            var measureIndex = int.Parse(string.Join("", marker.Text.Where(char.IsDigit)));
                            if (measureIndex != note.Measure.Index)
                            {
                                Debugger.Break();
                            }

                            break;
                        }
                    }

                    return cursor;
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

}