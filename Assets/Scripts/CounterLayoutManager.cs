using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class CounterLayoutManager : NetworkBehaviour
{
    public static CounterLayoutManager Instance { get; private set; }

    [Serializable]
    public struct CounterPrefabEntry
    {
        public NetworkPrefabRef prefab;
        public int count;
    }

    [SerializeField] private CounterLayoutDataSO layoutData;
    [SerializeField] private NetworkPrefabRef clearCounterPrefab;
    [SerializeField] private NetworkPrefabRef trashCounterPrefab;
    [SerializeField] private List<CounterPrefabEntry> counterPrefabs;

    [Networked] private NetworkBool LayoutSpawned { get; set; }

    public static void ResetStaticData()
    {
        Instance = null;
    }

    public override void Spawned()
    {
        Instance = this;
    }

    public void SpawnRandomizedLayout()
    {
        if (!HasStateAuthority || LayoutSpawned) return;

        List<int> cornerIndices = new List<int>();
        List<int> nonCornerIndices = new List<int>();

        for (int i = 0; i < layoutData.spawnPoints.Length; i++)
        {
            if (layoutData.spawnPoints[i].isCorner)
                cornerIndices.Add(i);
            else
                nonCornerIndices.Add(i);
        }

        // Spawn ClearCounters at corners
        foreach (int index in cornerIndices)
        {
            CounterLayoutDataSO.SpawnPoint sp = layoutData.spawnPoints[index];
            Runner.Spawn(clearCounterPrefab, sp.position, Quaternion.Euler(0f, sp.rotationY, 0f));
        }

        System.Random rng = new System.Random();

        // Find positions adjacent to corners (within 1.5 units along one axis)
        List<int> cornerAdjacentIndices = new List<int>();
        foreach (int ncIndex in nonCornerIndices)
        {
            Vector3 pos = layoutData.spawnPoints[ncIndex].position;
            foreach (int cIndex in cornerIndices)
            {
                Vector3 cornerPos = layoutData.spawnPoints[cIndex].position;
                float dx = Mathf.Abs(pos.x - cornerPos.x);
                float dz = Mathf.Abs(pos.z - cornerPos.z);
                if ((dx < 0.01f && Mathf.Abs(dz - 1.5f) < 0.01f) ||
                    (dz < 0.01f && Mathf.Abs(dx - 1.5f) < 0.01f))
                {
                    cornerAdjacentIndices.Add(ncIndex);
                    break;
                }
            }
        }

        // Place TrashCounter at a random corner-adjacent position
        int trashSlotIndex = cornerAdjacentIndices[rng.Next(cornerAdjacentIndices.Count)];
        CounterLayoutDataSO.SpawnPoint trashSp = layoutData.spawnPoints[trashSlotIndex];
        Runner.Spawn(trashCounterPrefab, trashSp.position, Quaternion.Euler(0f, trashSp.rotationY, 0f));

        // Build remaining non-corner positions (excluding trash slot)
        List<int> remainingIndices = new List<int>();
        foreach (int index in nonCornerIndices)
        {
            if (index != trashSlotIndex)
                remainingIndices.Add(index);
        }

        // Build flat list of prefab refs for remaining positions
        List<NetworkPrefabRef> prefabPool = new List<NetworkPrefabRef>();
        foreach (CounterPrefabEntry entry in counterPrefabs)
        {
            for (int i = 0; i < entry.count; i++)
            {
                prefabPool.Add(entry.prefab);
            }
        }

        // Fisher-Yates shuffle
        for (int i = prefabPool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (prefabPool[i], prefabPool[j]) = (prefabPool[j], prefabPool[i]);
        }

        // Spawn shuffled counters at remaining positions
        for (int i = 0; i < remainingIndices.Count; i++)
        {
            CounterLayoutDataSO.SpawnPoint sp = layoutData.spawnPoints[remainingIndices[i]];
            Runner.Spawn(prefabPool[i], sp.position, Quaternion.Euler(0f, sp.rotationY, 0f));
        }

        LayoutSpawned = true;
    }
}
