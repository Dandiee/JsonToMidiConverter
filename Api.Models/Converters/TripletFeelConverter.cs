using Api.Models.Enums;

namespace Api.Models.Converters;

public class TripletFeelConverter : MappedEnumConverter<TripletFeel>
{
    private static readonly IReadOnlyDictionary<string, TripletFeel> InternalMapping = new Dictionary<string, TripletFeel>(StringComparer.OrdinalIgnoreCase)
    {
        ["off"] = TripletFeel.Off,

        ["8th"] = TripletFeel.Eights,
        ["16th"] = TripletFeel.Sixteen,

        ["dotted8th"] = TripletFeel.DottedEight,
        ["dotted16th"] = TripletFeel.DottedSixteens,

        ["scottish8th"] = TripletFeel.ScottishEight,
        ["scottish16th"] = TripletFeel.ScottishSixteens,
    };

    protected override IReadOnlyDictionary<string, TripletFeel> Mapping => InternalMapping;
}