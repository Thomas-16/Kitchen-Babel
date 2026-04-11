using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class MapRandomizer : NetworkBehaviour
{
    public static MapRandomizer Instance { get; private set; }

    [Networked] public int RandomSeed { get; set; }

    private ChangeDetector changeDetector;
    private bool hasAppliedRandomization;

    public override void Spawned()
    {
        Instance = this;
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            RandomSeed = UnityEngine.Random.Range(1, int.MaxValue);
            ApplyRandomization();
        }
    }

    public override void Render()
    {
        if (hasAppliedRandomization) return;

        foreach (string propertyName in changeDetector.DetectChanges(this))
        {
            if (propertyName == nameof(RandomSeed) && RandomSeed != 0)
            {
                ApplyRandomization();
            }
        }
    }

    private void ApplyRandomization()
    {
        if (hasAppliedRandomization) return;
        if (RandomSeed == 0) return;

        hasAppliedRandomization = true;
        System.Random rng = new System.Random(RandomSeed);

        ShuffleResourceCounters(rng);

        if (HasStateAuthority)
        {
            SpawnFlexCounters(rng);
        }
    }

    private void ShuffleResourceCounters(System.Random rng)
    {
        // Sort by name for deterministic ordering across host and clients
        ShuffleableCounter[] counters = FindObjectsByType<ShuffleableCounter>(FindObjectsSortMode.None)
            .OrderBy(c => c.gameObject.name, StringComparer.Ordinal)
            .ToArray();

        if (counters.Length <= 1) return;

        // Record original positions and rotations
        Vector3[] positions = new Vector3[counters.Length];
        Quaternion[] rotations = new Quaternion[counters.Length];

        for (int i = 0; i < counters.Length; i++)
        {
            positions[i] = counters[i].transform.position;
            rotations[i] = counters[i].transform.rotation;
        }

        // Fisher-Yates shuffle on the position/rotation arrays
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

        // Apply shuffled positions
        for (int i = 0; i < counters.Length; i++)
        {
            counters[i].transform.position = positions[i];
            counters[i].transform.rotation = rotations[i];
        }
    }

    private void SpawnFlexCounters(System.Random rng)
    {
        FlexCounter[] flexCounters = FindObjectsByType<FlexCounter>(FindObjectsSortMode.None)
            .OrderBy(c => c.gameObject.name, StringComparer.Ordinal)
            .ToArray();

        if (flexCounters.Length == 0) return;

        // Collect all unique prefab types across every flex counter
        HashSet<NetworkPrefabRef> allTypes = new HashSet<NetworkPrefabRef>();
        foreach (FlexCounter fc in flexCounters)
        {
            if (fc.PossibleCounters == null) continue;
            foreach (NetworkPrefabRef prefab in fc.PossibleCounters)
                allTypes.Add(prefab);
        }

        // For each type, find which flex counter indices can provide it
        Dictionary<NetworkPrefabRef, List<int>> typeToProviders = new Dictionary<NetworkPrefabRef, List<int>>();
        foreach (NetworkPrefabRef type in allTypes)
        {
            List<int> providers = new List<int>();
            for (int i = 0; i < flexCounters.Length; i++)
            {
                if (flexCounters[i].PossibleCounters == null) continue;
                if (flexCounters[i].PossibleCounters.Contains(type))
                    providers.Add(i);
            }
            typeToProviders[type] = providers;
        }

        // Assign one flex counter per type, most-constrained types first
        NetworkPrefabRef[] assignments = new NetworkPrefabRef[flexCounters.Length];
        bool[] assigned = new bool[flexCounters.Length];
        HashSet<NetworkPrefabRef> coveredTypes = new HashSet<NetworkPrefabRef>();

        List<NetworkPrefabRef> sortedTypes = typeToProviders.Keys
            .OrderBy(t => typeToProviders[t].Count)
            .ToList();

        foreach (NetworkPrefabRef type in sortedTypes)
        {
            // Find unassigned flex counters that can provide this type
            List<int> available = typeToProviders[type]
                .Where(i => !assigned[i])
                .ToList();

            if (available.Count == 0) continue;

            int chosen = available[rng.Next(available.Count)];
            assignments[chosen] = type;
            assigned[chosen] = true;
            coveredTypes.Add(type);
        }

        // Fill remaining unassigned flex counters randomly
        for (int i = 0; i < flexCounters.Length; i++)
        {
            if (assigned[i]) continue;

            NetworkPrefabRef[] options = flexCounters[i].PossibleCounters;
            if (options == null || options.Length == 0) continue;

            assignments[i] = options[rng.Next(options.Length)];
            assigned[i] = true;
        }

        // Spawn all assignments
        for (int i = 0; i < flexCounters.Length; i++)
        {
            if (!assigned[i]) continue;

            Runner.Spawn(
                assignments[i],
                flexCounters[i].transform.position,
                flexCounters[i].transform.rotation
            );

            Destroy(flexCounters[i].gameObject);
        }
    }
}
