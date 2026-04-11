using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>
/// Static utility class for map randomization logic.
/// Called by MapManager during counter spawning.
/// </summary>
public class MapRandomizer
{
    /// <summary>
    /// Resolves which counter prefab each placeholder should spawn.
    /// For non-flex placeholders, returns their counterPrefab directly.
    /// For flex placeholders, uses constraint-satisfaction to ensure type coverage.
    /// </summary>
    public static NetworkPrefabRef[] ResolveFlexChoices(CounterPlaceholder[] placeholders, System.Random rng)
    {
        NetworkPrefabRef[] resolved = new NetworkPrefabRef[placeholders.Length];

        // Identify flex and non-flex placeholders
        List<int> flexIndices = new List<int>();
        for (int i = 0; i < placeholders.Length; i++)
        {
            if (placeholders[i].IsFlex)
            {
                flexIndices.Add(i);
            }
            else
            {
                resolved[i] = placeholders[i].CounterPrefab;
            }
        }

        if (flexIndices.Count == 0) return resolved;

        // Collect all unique prefab types across flex placeholders
        HashSet<NetworkPrefabRef> allTypes = new HashSet<NetworkPrefabRef>();
        foreach (int idx in flexIndices)
        {
            NetworkPrefabRef[] options = placeholders[idx].FlexOptions;
            if (options == null) continue;
            foreach (NetworkPrefabRef prefab in options)
                allTypes.Add(prefab);
        }

        // For each type, find which flex indices can provide it
        Dictionary<NetworkPrefabRef, List<int>> typeToProviders = new Dictionary<NetworkPrefabRef, List<int>>();
        foreach (NetworkPrefabRef type in allTypes)
        {
            List<int> providers = new List<int>();
            foreach (int idx in flexIndices)
            {
                NetworkPrefabRef[] options = placeholders[idx].FlexOptions;
                if (options == null) continue;
                if (options.Contains(type))
                    providers.Add(idx);
            }
            typeToProviders[type] = providers;
        }

        // Assign one flex placeholder per type, most-constrained types first
        bool[] assigned = new bool[placeholders.Length];

        List<NetworkPrefabRef> sortedTypes = typeToProviders.Keys
            .OrderBy(t => typeToProviders[t].Count)
            .ToList();

        foreach (NetworkPrefabRef type in sortedTypes)
        {
            List<int> available = typeToProviders[type]
                .Where(i => !assigned[i])
                .ToList();

            if (available.Count == 0) continue;

            int chosen = available[rng.Next(available.Count)];
            resolved[chosen] = type;
            assigned[chosen] = true;
        }

        // Fill remaining unassigned flex placeholders randomly
        foreach (int idx in flexIndices)
        {
            if (assigned[idx]) continue;

            NetworkPrefabRef[] options = placeholders[idx].FlexOptions;
            if (options == null || options.Length == 0) continue;

            resolved[idx] = options[rng.Next(options.Length)];
            assigned[idx] = true;
        }

        return resolved;
    }

    /// <summary>
    /// Fisher-Yates shuffles the positions and rotations of shuffleable placeholders in-place.
    /// Call this before spawning counters so they spawn at shuffled positions.
    /// </summary>
    public static void ShufflePlaceholderPositions(CounterPlaceholder[] placeholders, System.Random rng)
    {
        // Collect only shuffleable placeholders
        List<CounterPlaceholder> shuffleable = new List<CounterPlaceholder>();
        foreach (CounterPlaceholder p in placeholders)
        {
            if (p.IsShuffleable)
                shuffleable.Add(p);
        }

        if (shuffleable.Count <= 1) return;

        // Record original positions and rotations
        Vector3[] positions = new Vector3[shuffleable.Count];
        Quaternion[] rotations = new Quaternion[shuffleable.Count];

        for (int i = 0; i < shuffleable.Count; i++)
        {
            positions[i] = shuffleable[i].transform.position;
            rotations[i] = shuffleable[i].transform.rotation;
        }

        // Fisher-Yates shuffle
        for (int i = positions.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);

            Vector3 tempPos = positions[i];
            positions[i] = positions[j];
            positions[j] = tempPos;

            Quaternion tempRot = rotations[i];
            rotations[i] = rotations[j];
            rotations[j] = tempRot;
        }

        // Apply shuffled positions back to placeholder transforms
        for (int i = 0; i < shuffleable.Count; i++)
        {
            shuffleable[i].transform.position = positions[i];
            shuffleable[i].transform.rotation = rotations[i];
        }
    }
}
