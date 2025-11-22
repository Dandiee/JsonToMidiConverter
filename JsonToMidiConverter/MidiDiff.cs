using Melanchall.DryWetMidi.Core;

internal static class MidiDiff
{
    public static void Report(string expectedPath, string actualPath)
    {
        try
        {
            var expected = MidiFile.Read(expectedPath);
            var actual = MidiFile.Read(actualPath);

            var expectedTracks = expected.GetTrackChunks().ToArray();
            var actualTracks = actual.GetTrackChunks().ToArray();

            Console.WriteLine($"Reference tracks: {expectedTracks.Length} | Output tracks: {actualTracks.Length}");

            var trackCount = Math.Max(expectedTracks.Length, actualTracks.Length);
            for (var index = 0; index < trackCount; index++)
            {
                var expectedTrack = index < expectedTracks.Length ? expectedTracks[index] : null;
                var actualTrack = index < actualTracks.Length ? actualTracks[index] : null;

                var expectedName = ExtractTrackName(expectedTrack);
                var actualName = ExtractTrackName(actualTrack);

                var expectedStats = TrackStats.Create(expectedTrack);
                var actualStats = TrackStats.Create(actualTrack);

                Console.WriteLine(
                    $"Track {index:D2}: ref='{expectedName}' (events={expectedStats.Total}, noteOn={expectedStats.NoteOn}, noteOff={expectedStats.NoteOff}, pb={expectedStats.PitchBend}, cc={expectedStats.ControlChange}) | " +
                    $"out='{actualName}' (events={actualStats.Total}, noteOn={actualStats.NoteOn}, noteOff={actualStats.NoteOff}, pb={actualStats.PitchBend}, cc={actualStats.ControlChange})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to compute MIDI diff: {ex.Message}");
        }
    }

    private static string ExtractTrackName(TrackChunk? chunk)
    {
        if (chunk is null)
        {
            return "<missing>";
        }

        var nameEvent = chunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault();
        return string.IsNullOrWhiteSpace(nameEvent?.Text) ? "(unnamed)" : nameEvent.Text;
    }

    private sealed record TrackStats(int Total, int NoteOn, int NoteOff, int PitchBend, int ControlChange)
    {
        public static TrackStats Create(TrackChunk? chunk)
        {
            if (chunk is null)
            {
                return new TrackStats(0, 0, 0, 0, 0);
            }

            var events = chunk.Events;
            return new TrackStats(
                events.Count,
                events.Count(e => e is NoteOnEvent),
                events.Count(e => e is NoteOffEvent),
                events.Count(e => e is PitchBendEvent),
                events.Count(e => e is ControlChangeEvent));
        }
    }
}
