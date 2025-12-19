using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models;

[AutoSerialize]
public sealed partial class Automations : Serializable
{
    public List<AutomationTempo> Tempo { get; set; } = [];
}