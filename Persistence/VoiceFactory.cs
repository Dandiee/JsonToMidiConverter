using Api.Models;
using Persistence.Models;

namespace Persistence;

public static class VoiceFactory
{
    public static Voice FromRaw(RawVoice raw)
    {
        var model = ThreadLocalPool<Voice>.Rent();

        model.Beats = raw.Beats.Select(BeatFactory.FromRaw).ToList();
        model.Rest = raw.Rest;
        model.HasSameRhythm = raw.HasSameRhythm;

        return model;
    }
}