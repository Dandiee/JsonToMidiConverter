namespace Dani.Data.Generators;

internal class PropModel
{
    public string Name;
    public string TypeName;
    public string CleanTypeName;

    public bool IsBool;
    public bool IsEnum;
    public bool IsList;
    public string ListInnerType;

    // Generator-Specific Flags (Set these in the specific generator logic)
    public int Bits;
    public bool IsPackedNull;
    public string CastForBitPacking; // Used by Index Generator
}