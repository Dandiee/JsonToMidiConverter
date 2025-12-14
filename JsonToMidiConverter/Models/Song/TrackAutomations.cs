namespace JsonToMidiConverter.Models.Song;

public sealed class TrackAutomations
{
    public TrackSoundAutomation[] TrackSoundAutomations { get; set; } = [];
}

public sealed class TrackSoundAutomation
{
    public int InstrumentId { get; set; }
    public ushort Measure { get; set; }
    public int Position { get; set; }
}