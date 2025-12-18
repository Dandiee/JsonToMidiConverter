using Api.Models;
using Persistence.Models;

namespace Persistence;

public static class VoiceFactory
{
    public static Voice FromRaw(RawVoice raw) => new()
    {
        Beats = raw.Beats.Select(BeatFactory.FromRaw).ToList(),

        Rest = raw.Rest,
        HasSameRhythm = raw.HasSameRhythm,
    };
}