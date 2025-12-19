using Api.Generators;

namespace Api.Models;

[AutoSerialize]
public sealed partial class Automations : Serializable
{
    public List<AutomationTempo> Tempo { get; set; } = [];

    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    Tempo = ReadList<AutomationTempo>(buffer, ref cursor);
    //}

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    Write(buffer, ref cursor, Tempo);
    //}
}