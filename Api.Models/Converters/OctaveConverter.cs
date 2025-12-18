using Api.Models.Enums;

namespace Api.Models.Converters;

public class OctaveConverter : MappedEnumConverter<Octave>
{
    private static readonly IReadOnlyDictionary<string, Octave> InternalMapping = new Dictionary<string, Octave>
    {
        ["8va"] = Octave.Higher,
        ["8vb"] = Octave.Lower
    };

    protected override IReadOnlyDictionary<string, Octave> Mapping => InternalMapping;
}