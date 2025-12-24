using System.Diagnostics;
using Dani.Data.Models;
using DataPart = Dani.Data.Models.Parts.Part;

namespace Dani.Converter.Models;

[DebuggerDisplay("{Record.Artist!} - {Record.Title!}")]
public sealed class Song
{
    public Record Record { get; }
    public List<Part> Parts { get; }

    public Song(Record record, IReadOnlyList<DataPart> parts)
    {
        Record = record;
        Parts = parts.Select(e => new Part(this, e)).ToList();
    }
}