using System.Text;

namespace JsonToMidiConverter;

public class FlagCorrelationAnalyzer
{
    private readonly string[] _names;
    // Stores every unique combination seen (as a bitmask) and how often it appeared
    private readonly Dictionary<int, int> _observedStates = new();
    private int _totalRows = 0;

    public FlagCorrelationAnalyzer(params string[] flagNames)
    {
        _names = flagNames;
    }

    public void Ingest(bool[] flags)
    {
        if (flags.Length != _names.Length)
            throw new ArgumentException("Flag count must match name count");

        int mask = 0;
        for (int i = 0; i < flags.Length; i++)
        {
            if (flags[i]) mask |= (1 << i);
        }

        if (!_observedStates.ContainsKey(mask)) _observedStates[mask] = 0;
        _observedStates[mask]++;
        _totalRows++;
    }

    public string GenerateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- Analysis of {_totalRows} rows ---");

        var distinctStates = _observedStates.Keys.ToList();

        // Check every pair of flags
        for (int i = 0; i < _names.Length; i++)
        {
            for (int j = 0; j < _names.Length; j++)
            {
                if (i == j) continue;

                bool neverTogether = true;      // Mutual Exclusion
                bool alwaysTogether = true;     // Equivalence
                bool iImpliesJ = true;          // Implication (if I then J)

                foreach (var mask in distinctStates)
                {
                    bool iSet = (mask & (1 << i)) != 0;
                    bool jSet = (mask & (1 << j)) != 0;

                    if (iSet && jSet) neverTogether = false;
                    if (iSet != jSet) alwaysTogether = false;
                    if (iSet && !jSet) iImpliesJ = false;
                }

                // Output meaningful relationships
                if (j > i && alwaysTogether) // j > i prevents printing A==B and B==A
                    sb.AppendLine($"[EQUIVALENT] {_names[i]} == {_names[j]} (They are always the same)");

                else if (j > i && neverTogether)
                    sb.AppendLine($"[EXCLUSIVE]  {_names[i]} and {_names[j]} are NEVER true at the same time");

                else if (iImpliesJ && !alwaysTogether)
                    sb.AppendLine($"[IMPLIES]    If {_names[i]} is true => {_names[j]} is ALWAYS true");
            }
        }

        // Optional: Detect flags that are literally never true
        for (int i = 0; i < _names.Length; i++)
        {
            bool everSeen = distinctStates.Any(mask => (mask & (1 << i)) != 0);
            if (!everSeen) sb.AppendLine($"[DEAD]       {_names[i]} was never true.");
        }

        return sb.ToString();
    }
}