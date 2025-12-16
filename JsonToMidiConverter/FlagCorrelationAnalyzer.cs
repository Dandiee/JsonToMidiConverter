using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class FlagCorrelationAnalyzer
{
    private readonly string[] _names;

    // Thread-safe collection. Key = Bitmask, Value = Count
    private readonly ConcurrentDictionary<int, int> _observedStates = new();

    public FlagCorrelationAnalyzer(params string[] flagNames)
    {
        if (flagNames.Length > 32)
            throw new ArgumentException("Analyzer supports a maximum of 32 flags due to integer bitmask limits.");

        _names = flagNames;
    }

    // Completely thread-safe, lock-free ingestion
    public void Ingest(bool[] flags)
    {
        if (flags.Length != _names.Length)
            throw new ArgumentException("Flag count must match name count");

        int mask = 0;
        for (int i = 0; i < flags.Length; i++)
        {
            if (flags[i]) mask |= (1 << i);
        }

        // Atomically adds the key if missing, or increments the value if present.
        // This handles high contention from 32 threads efficiently.
        _observedStates.AddOrUpdate(key: mask, addValue: 1, updateValueFactory: (key, oldValue) => oldValue + 1);
    }

    public string GenerateReport()
    {
        // 1. Create a point-in-time snapshot.
        // This ensures that 'totalRows' is consistent with 'distinctStates'
        // even if other threads are still calling Ingest().
        var snapshot = _observedStates.ToArray();

        long totalRows = 0;
        foreach (var kvp in snapshot)
        {
            totalRows += kvp.Value;
        }

        // Get just the keys (masks) from the snapshot
        var distinctStates = snapshot.Select(x => x.Key).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"--- Analysis of {totalRows} rows (Snapshot) ---");

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

                    // Optimization: If all flags are already false, we can stop checking this pair against other masks
                    if (!neverTogether && !alwaysTogether && !iImpliesJ) break;
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

        // Detect flags that are literally never true
        for (int i = 0; i < _names.Length; i++)
        {
            bool everSeen = distinctStates.Any(mask => (mask & (1 << i)) != 0);
            if (!everSeen) sb.AppendLine($"[DEAD]       {_names[i]} was never true.");
        }

        return sb.ToString();
    }
}