using System.Collections.Generic;
using System.Linq;

namespace Dani.Data.Generators;

internal class ClassModel
{
    public string Name;
    public string Namespace;

    // A single unified list
    public List<PropModel> Properties { get; set; } = new();

    // --- Helper Accessors for the Generators ---

    // Used by IndexSerializationGenerator (Props that get packed into the Key)
    public IEnumerable<PropModel> GetIndexableProps() =>
        Properties.Where(p => p.Bits > 0).OrderBy(p => p.Name);

    // Used by IndexSerializationGenerator (Props that are written to the buffer payload)
    public IEnumerable<PropModel> GetReferenceProps() =>
        Properties.Where(p => p.Bits == 0).OrderBy(p => p.Name);
}