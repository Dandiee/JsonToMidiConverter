using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Interaction;

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

    public static void Dump(Song song, string midiPath, string? artist)
    {
        var midi = MidiFile.Read(midiPath);
        var output = ProcessMidi(song, midi);

        File.WriteAllText($"Logs_{artist}", output);
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

                        
                    }

                    if (beat.MidiEventIndex.HasValue)
                    {
                        var from = beat.MidiEventIndex.Value;
                        var to = beat.MidiEventCount.HasValue
                            ? beat.MidiEventCount.Value + from
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
                                sb.AppendLine($"\t\t\t..... More pitch bending");
                            }
                            else if (pitchCounter < 3)
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

        return sb.ToString();
    }

    public static void SetSongMidiEvents(Song song, MidiFile midi)
    {
        var chunks = midi.Chunks.OfType<TrackChunk>().ToList();

        foreach (var part in song.Parts)
        {
            var chunk = chunks[part.Index];
            var events = chunk.Events.ToList();

            Beat? lastMarkedBeat = null;
            var cursor = 0;

            foreach (var measure in part.Measures)
            {
                foreach (var beat in measure.Beats)
                {
                    if (beat.Rest || beat.Notes.All(e => e.Rest || e.Tie)) continue;

                    beat.MidiEventIndex = GetBeatEventCursor(events, ref cursor, beat);

                    if (lastMarkedBeat != null)
                    {
                        lastMarkedBeat.MidiEventCount = cursor - lastMarkedBeat.MidiEventIndex!.Value;
                    }

                    lastMarkedBeat = beat;
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

    public static int GetBeatEventCursor(List<MidiEvent> events, ref int cursor, Beat beat)
    {
        if (beat.Is(1772))
        {

        }

        do
        {
            cursor++;
        } while (!IsMatchingBeat(events, cursor, beat));

        if (!IsMatchingMeasure(events, cursor, beat, out var m1))
        {
            Debugger.Break();
        }

        var result = cursor;

        //foreach (var otherNote in note.Beat.Notes.Where(e => e != note && !e.Tie))
        //{
        //    do
        //    {
        //        cursor++;
        //    } while (!IsMatchingBeat(events, cursor, otherNote));

        //    if (!IsMatchingMeasure(events, cursor, note, out var m2))
        //    {
        //        Debugger.Break();
        //    }
        //}

        return result;
    }

    public static bool IsMatchingMeasure(List<MidiEvent> events, int cursor, Beat beat, out int measureIndex)
    {
        var measureCursor = cursor;
        for (var i = measureCursor; ; i--)
        {
            var ev = events[i];
            if (ev is MarkerEvent marker)
            {
                measureIndex = int.Parse(string.Join("", marker.Text.Where(char.IsDigit)));
                return measureIndex == beat.Measure.Index;
            }
        }
    }

    public static bool IsMatchingBeat(List<MidiEvent> events, int cursor, Beat beat)
    {
        if (beat.Notes.Count(e => !e.Tie && !e.Rest) == 1)
        {
            var note = beat.Notes.Single(e => !e.Tie && !e.Rest);

            if (!IsMatchingNote(events, cursor, note)) return false;
            if (!IsPitchClearing(events, cursor - 1, note)) return false;
        }
        else
        {
            if (beat.Part.IsPianoLike)
            {
                for (var i = 0; i < beat.Notes.Length; i++)
                {
                    var noteOnIndex = cursor + i;
                    var pitchClearIndex = noteOnIndex - beat.Notes.Length;

                    if (!IsMatchingNote(events, noteOnIndex, beat.Notes[i])) return false;
                    if (!IsPitchClearing(events, pitchClearIndex, beat.Notes[i])) return false;
                }
            }
            else
            {
                var noteOffs = 0;

                for (var i = 0; i < beat.Notes.Length; i++)
                {
                    var noteOnIndex = cursor + i * 2 + noteOffs;
                    var pitchClearIndex = noteOnIndex - 1;

                    var currentNoteOffs = 0;
                    while (events[pitchClearIndex + currentNoteOffs] is NoteOffEvent)
                    {
                        currentNoteOffs++;
                    }

                    noteOffs += currentNoteOffs;
                    noteOnIndex = cursor + i * 2 + noteOffs;
                    pitchClearIndex = noteOnIndex - 1;

                    if (!IsMatchingNote(events, noteOnIndex, beat.Notes[i])) return false;
                    if (!IsPitchClearing(events, pitchClearIndex, beat.Notes[i])) return false;
                }

            }
        }

        return true;
    }

    private static bool IsPitchClearing(List<MidiEvent> events, int cursor, Nóta note)
    {
        var midiEvent = events[cursor];
        if (midiEvent is not PitchBendEvent pitch) return false;
        if (pitch.PitchValue != 8192) return false;
        if (pitch.Channel != note.Channel) return false;

        return true;
    }

    private static bool IsMatchingNote(List<MidiEvent> events, int cursor, Nóta note)
    {
        var midiEvent = events[cursor];
        if (midiEvent is not NoteOnEvent on) return false;
        if (on.DeltaTime != 0 || on.NoteNumber != note.NoteNumber) return false;

        return true;
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