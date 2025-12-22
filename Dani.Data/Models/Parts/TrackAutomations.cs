using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class TrackAutomations : Serializable
{
    public List<TrackSoundAutomation> TrackSoundAutomations { get; set; } = [];
}

[AutoSerialize]
public sealed partial class TrackSoundAutomation : Serializable
{
    public int InstrumentId { get; set; }
    public ushort Measure { get; set; }
    public int Position { get; set; }
}