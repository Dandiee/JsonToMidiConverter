using System.Text.Json.Serialization;
using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models;

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