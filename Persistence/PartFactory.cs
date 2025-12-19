using Api.Models;
using Persistence.Models;
namespace Persistence;

public static class PartFactory
{
    public static Part FromRaw(RawPart raw)
    {
        var model = ThreadLocalPool<Part>.Rent();

        model.Measures = raw.Measures.Select(MeasureFactory.FromRaw).ToList();
        model.Index = raw.Index;
        model.Name = raw.Name;
        model.Instrument = raw.Instrument;
        model.Balance = raw.Balance;
        model.Volume = raw.Volume;
        model.Frets = raw.Frets;
        model.Strings = raw.Strings;
        model.InstrumentId = raw.InstrumentId;
        model.PartId = raw.PartId;
        model.Version = raw.Version;
        model.SongId = raw.SongId;
        model.RevisionId = raw.RevisionId;
        model.Capo = raw.Capo;
        model.Voices = raw.Voices;
        model.WithLyrics = raw.WithLyrics;
        model.TuningFlat = raw.TuningFlat;
        model.Anacrusis = raw.Anacrusis;
        model.TuningShortDrone = raw.TuningShortDrone;
        model.CapoPartial = raw.CapoPartial;
        model.TrackAutomations = raw.TrackAutomations;
        model.Automations = raw.Automations;
        model.Tuning = raw.Tuning;
        model.NewLyrics = raw.NewLyrics;

        return model;
    }
}