using Api.Models;
using Api.Models.Enums;
using Persistence.Models;
namespace Persistence;

public static class PartFactory
{
    public static Part FromRaw(RawPart raw) => new()
    {
        Measures = raw.Measures.Select(MeasureFactory.FromRaw).ToList(),

        // Primitives - Direct Copy
        Name = raw.Name,
        Instrument = raw.Instrument,
        Balance = raw.Balance,
        Volume = raw.Volume,
        Frets = raw.Frets,
        Strings = raw.Strings,
        InstrumentId = raw.InstrumentId,
        PartId = raw.PartId,
        Version = raw.Version,
        SongId = raw.SongId,
        RevisionId = raw.RevisionId,
        Capo = raw.Capo,
        Voices = raw.Voices,
        WithLyrics = raw.WithLyrics,
        TuningFlat = raw.TuningFlat,
        Anacrusis = raw.Anacrusis,
        TuningShortDrone = raw.TuningShortDrone,

        // Simple Objects - Direct Copy (or clone if mutable)
        CapoPartial = raw.CapoPartial,
        TrackAutomations = raw.TrackAutomations,
        Automations = raw.Automations,

        // Collections - Create NEW lists to break reference to raw data
        // (Assuming you have sanitizers for Measure and Lyric)
        Tuning = raw.Tuning,
        NewLyrics = raw.NewLyrics
    };
}