using Melanchall.DryWetMidi.Core;
using System;
using System.Linq;

class DetailedCompare
{
    public static void Run()
    {
        var reference = MidiFile.Read("ReferenceOutput.mid");
        var output = MidiFile.Read("Output.mid");
        
        Console.WriteLine("=== DETAILED TRACK 0 COMPARISON ===\n");
        
        var refTrack = reference.GetTrackChunks().First();
        var outTrack = output.GetTrackChunks().First();
        
        Console.WriteLine($"Reference events: {refTrack.Events.Count}");
        Console.WriteLine($"Output events: {outTrack.Events.Count}");
        Console.WriteLine();
        
        // Show first 40 events side by side
        Console.WriteLine("First 40 events:");
        for (int i = 0; i < Math.Min(40, Math.Max(refTrack.Events.Count, outTrack.Events.Count)); i++)
        {
            var refEvent = i < refTrack.Events.Count ? refTrack.Events[i] : null;
            var outEvent = i < outTrack.Events.Count ? outTrack.Events[i] : null;
            
            var refStr = FormatEvent(refEvent);
            var outStr = FormatEvent(outEvent);
            
            var marker = refStr == outStr ? "✓" : "✗";
            Console.WriteLine($"{i,3} {marker} REF: {refStr}");
            if (refStr != outStr)
                Console.WriteLine($"    {marker} OUT: {outStr}");
        }
        
        // Analyze event type distribution
        Console.WriteLine("\n=== EVENT TYPE DISTRIBUTION ===");
        var refTypes = refTrack.Events.GroupBy(e => e.GetType().Name).OrderBy(g => g.Key);
        var outTypes = outTrack.Events.GroupBy(e => e.GetType().Name).OrderBy(g => g.Key);
        
        var allTypes = refTypes.Select(g => g.Key).Union(outTypes.Select(g => g.Key)).OrderBy(t => t);
        
        foreach (var type in allTypes)
        {
            var refCount = refTypes.FirstOrDefault(g => g.Key == type)?.Count() ?? 0;
            var outCount = outTypes.FirstOrDefault(g => g.Key == type)?.Count() ?? 0;
            var diff = outCount - refCount;
            var marker = diff == 0 ? "✓" : (diff > 0 ? "+" : "-");
            Console.WriteLine($"{marker} {type,-30} Ref: {refCount,4}  Out: {outCount,4}  Diff: {diff,4}");
        }
    }
    
    static string FormatEvent(MidiEvent? evt)
    {
        if (evt == null) return "(missing)";
        
        var delta = $"@{evt.DeltaTime,6}";
        
        return evt switch
        {
            NoteOnEvent noteOn => $"{delta} NoteOnEvent  Nóta={noteOn.NoteNumber,3} Vel={noteOn.Velocity,3} Ch={noteOn.Channel}",
            NoteOffEvent noteOff => $"{delta} NoteOff Nóta={noteOff.NoteNumber,3} Ch={noteOff.Channel}",
            SetTempoEvent tempo => $"{delta} Tempó   {tempo.MicrosecondsPerQuarterNote}µs/qn",
            TimeSignatureEvent ts => $"{delta} TimeSig {ts.Numerator}/{Math.Pow(2, ts.Denominator)}",
            ProgramChangeEvent pc => $"{delta} Program {pc.ProgramNumber,3} Ch={pc.Channel}",
            SequenceTrackNameEvent name => $"{delta} Track   '{name.Text}'",
            InstrumentNameEvent inst => $"{delta} Instrument '{inst.Text}'",
            MarkerEvent marker => $"{delta} Marker  '{marker.Text}'",
            LyricEvent lyric => $"{delta} Lyric   '{lyric.Text}'",
            _ => $"{delta} {evt.GetType().Name}"
        };
    }
}
