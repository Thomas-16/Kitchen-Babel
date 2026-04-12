using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class MapManager : NetworkBehaviour
{
    public static MapManager Instance { get; private set; }
    public static event Action OnMapLoaded;

    [SerializeField] private MapListSO mapList;

    [Networked] public int MapIndex { get; set; } = -1;
    [Networked] public int RandomSeed { get; set; }
    [Networked] public NetworkBool MapReady { get; set; }

    private ChangeDetector changeDetector;
    private bool hasLoadedMap;
    private bool hasFinishedSetup;
    private GameObject instantiatedMap;
    private List<Transform> cachedSpawnPositions;

    public override void Spawned()
    {
        Instance = this;
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            // Only pick a new map if this is a fresh game (not host migration restoring state)
            if (MapIndex < 0)
            {
                MapIndex = UnityEngine.Random.Range(0, mapList.maps.Count);
                RandomSeed = UnityEngine.Random.Range(1, int.MaxValue);
            }
        }

        // If MapIndex is already set (host migration, late join, or host just set it), load immediately
        if (MapIndex >= 0)
        {
            LoadMap();
        }
    }

    public override void Render()
    {
        if (hasLoadedMap) return;

        foreach (string propertyName in changeDetector.DetectChanges(this))
        {
            if (propertyName == nameof(MapIndex) && MapIndex >= 0)
            {
                LoadMap();
            }
        }
    }

    private void LoadMap()
    {
        if (hasLoadedMap) return;
        if (MapIndex < 0 || MapIndex >= mapList.maps.Count) return;

        hasLoadedMap = true;
        MapConfigSO config = mapList.maps[MapIndex];

        // Instantiate the map prefab (walls, floor, decorations, placeholders)
        instantiatedMap = Instantiate(config.mapPrefab);

        // Apply camera settings
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = config.cameraPosition;
            mainCamera.transform.eulerAngles = config.cameraRotation;
            mainCamera.fieldOfView = config.cameraFOV;
        }

        // Cache spawn positions from the map prefab
        Transform spawnPositionsParent = instantiatedMap.transform.Find("PlayerSpawnPositions");
        if (spawnPositionsParent != null)
        {
            cachedSpawnPositions = new List<Transform>();
            foreach (Transform child in spawnPositionsParent)
            {
                cachedSpawnPositions.Add(child);
            }
        }

        // If KitchenGameMultiplayer is already ready, finish setup now
        if (KitchenGameMultiplayer.Instance != null)
        {
            FinishSetup();
        }
        else
        {
            // Wait for KitchenGameMultiplayer to be ready
            KitchenGameMultiplayer.OnInstanceReady += OnKitchenGameMultiplayerReady;
        }

        OnMapLoaded?.Invoke();
    }

    private void OnKitchenGameMultiplayerReady()
    {
        KitchenGameMultiplayer.OnInstanceReady -= OnKitchenGameMultiplayerReady;
        FinishSetup();
    }

    private void FinishSetup()
    {
        if (hasFinishedSetup) return;
        hasFinishedSetup = true;
        Debug.Log($"[MapManager] FinishSetup — HasStateAuthority={HasStateAuthority}, MapReady={MapReady}");

        if (cachedSpawnPositions != null)
        {
            KitchenGameMultiplayer.Instance.SetSpawnPositions(cachedSpawnPositions);
        }

        // Host spawns counters and triggers game start
        // MapReady check: during host migration, counters are restored from snapshot — skip re-spawning
        if (HasStateAuthority && !MapReady)
        {
            SpawnCounters();
            MapReady = true;

            KitchenGameMultiplayer.Instance.SpawnAllPlayers();

            // Start the game once KitchenGameManager is ready
            if (KitchenGameManager.Instance != null)
            {
                Debug.Log("[MapManager] KitchenGameManager ready — calling TryStartGame()");
                KitchenGameManager.Instance.TryStartGame();
            }
            else
            {
                Debug.Log("[MapManager] KitchenGameManager not ready — subscribing to OnInstanceReady");
                KitchenGameManager.OnInstanceReady += OnKitchenGameManagerReady;
            }

        }
        else
        {
            Debug.Log($"[MapManager] Skipping counter spawn — not authority or already ready");
        }
    }

    private void OnKitchenGameManagerReady(object sender, EventArgs e)
    {
        KitchenGameManager.OnInstanceReady -= OnKitchenGameManagerReady;
        Debug.Log("[MapManager] KitchenGameManager now ready — calling TryStartGame()");
        KitchenGameManager.Instance.TryStartGame();
    }

    private void SpawnCounters()
    {
        CounterPlaceholder[] placeholders = instantiatedMap.GetComponentsInChildren<CounterPlaceholder>()
            .OrderBy(p => p.gameObject.name, StringComparer.Ordinal)
            .ToArray();

        if (placeholders.Length == 0) return;

        System.Random rng = new System.Random(RandomSeed);

        // Resolve flex choices first (uses RNG)
        NetworkPrefabRef[] resolvedPrefabs = MapRandomizer.ResolveFlexChoices(placeholders, rng);

        // Shuffle shuffleable placeholder positions (uses same RNG sequence)
        MapRandomizer.ShufflePlaceholderPositions(placeholders, rng);

        // Spawn all counters at their final positions
        for (int i = 0; i < placeholders.Length; i++)
        {
            Vector3 pos = placeholders[i].transform.position;
            Quaternion rot = placeholders[i].transform.rotation;
            Runner.Spawn(
                resolvedPrefabs[i],
                pos,
                rot,
                null,
                (runner, obj) =>
                {
                    BaseCounter counter = obj.GetComponent<BaseCounter>();
                    if (counter != null)
                    {
                        counter.SpawnPosition = pos;
                        counter.SpawnRotation = rot;
                    }
                }
            );
        }

        // Clean up placeholder objects
        foreach (CounterPlaceholder placeholder in placeholders)
        {
            Destroy(placeholder.gameObject);
        }
    }
}
