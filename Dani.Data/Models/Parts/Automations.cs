using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class Automations : Serializable
{
    public List<AutomationTempo> Tempo { get; set; } = [];
}