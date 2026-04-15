using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Fusion;
using UnityEngine;

public class MapManager : NetworkBehaviour
{
    public const float VERSUS_KITCHEN_OFFSET_X = 1000f;

    public static MapManager Instance { get; private set; }
    public static event Action OnMapLoaded;

    [SerializeField] private MapListSO mapList;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private NetworkPrefabRef coopFallbackCounterPrefab;

    [Networked] public int MapIndex { get; set; } = -1;
    [Networked] public int RandomSeed { get; set; }
    [Networked] public NetworkBool MapReady { get; set; }
    [Networked] public NetworkBool IsVersus { get; set; }

    private ChangeDetector changeDetector;
    private bool hasLoadedMap;
    private bool hasFinishedSetup;

    // Co-op
    private GameObject instantiatedMap;
    private List<Transform> cachedSpawnPositions;

    // Versus
    private GameObject instantiatedMapTeamA;
    private GameObject instantiatedMapTeamB;
    private List<Transform> cachedSpawnPositionsTeamA;
    private List<Transform> cachedSpawnPositionsTeamB;

    public static Vector3 GetTeamOffset(int teamIndex)
    {
        return teamIndex == 1 ? new Vector3(VERSUS_KITCHEN_OFFSET_X, 0f, 0f) : Vector3.zero;
    }

    public override void Spawned()
    {
        Instance = this;
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            // Only pick a new map if this is a fresh game (not host migration restoring state)
            if (MapIndex < 0)
            {
                int chosen = NetworkRunnerManager.Instance != null
                    ? NetworkRunnerManager.Instance.SelectedMapIndex
                    : -1;
                MapIndex = (chosen >= 0 && chosen < mapList.maps.Count)
                    ? chosen
                    : UnityEngine.Random.Range(0, mapList.maps.Count);
                RandomSeed = UnityEngine.Random.Range(1, int.MaxValue);
                IsVersus = LobbyManager.GetCachedMatchMode() == LobbyManager.MatchModeVersus;
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

        if (IsVersus)
        {
            instantiatedMapTeamA = InstantiateKitchen(config, 0);
            instantiatedMapTeamB = InstantiateKitchen(config, 1);
            cachedSpawnPositionsTeamA = CollectSpawnPositions(instantiatedMapTeamA);
            cachedSpawnPositionsTeamB = CollectSpawnPositions(instantiatedMapTeamB);

            int localTeam = LobbyManager.GetCachedPlayerTeamIndex(Runner.LocalPlayer);
            if (localTeam != 0 && localTeam != 1) localTeam = 0;
            PositionVirtualCamera(config, GetTeamOffset(localTeam));
        }
        else
        {
            instantiatedMap = InstantiateKitchen(config, 0);
            cachedSpawnPositions = CollectSpawnPositions(instantiatedMap);
            PositionVirtualCamera(config, Vector3.zero);
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

    private GameObject InstantiateKitchen(MapConfigSO config, int teamIndex)
    {
        Vector3 offset = GetTeamOffset(teamIndex);
        GameObject instance = Instantiate(config.mapPrefab, offset, Quaternion.identity);
        return instance;
    }

    private static List<Transform> CollectSpawnPositions(GameObject mapInstance)
    {
        List<Transform> list = new List<Transform>();
        Transform spawnPositionsParent = mapInstance.transform.Find("PlayerSpawnPositions");
        if (spawnPositionsParent != null)
        {
            foreach (Transform child in spawnPositionsParent)
            {
                list.Add(child);
            }
        }
        return list;
    }

    private void PositionVirtualCamera(MapConfigSO config, Vector3 kitchenOffset)
    {
        if (virtualCamera == null) return;
        virtualCamera.transform.position = config.cameraPosition + kitchenOffset;
        virtualCamera.transform.eulerAngles = config.cameraRotation;
        virtualCamera.m_Lens.FieldOfView = config.cameraFOV;
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
        Debug.Log($"[MapManager] FinishSetup — HasStateAuthority={HasStateAuthority}, MapReady={MapReady}, IsVersus={IsVersus}");

        if (IsVersus)
        {
            if (cachedSpawnPositionsTeamA != null && cachedSpawnPositionsTeamB != null)
            {
                KitchenGameMultiplayer.Instance.SetSpawnPositionsVersus(cachedSpawnPositionsTeamA, cachedSpawnPositionsTeamB);
            }
        }
        else
        {
            if (cachedSpawnPositions != null)
            {
                KitchenGameMultiplayer.Instance.SetSpawnPositions(cachedSpawnPositions);
            }
        }

        // Host spawns counters and triggers game start
        // MapReady check: during host migration, counters are restored from snapshot — skip re-spawning
        if (HasStateAuthority && !MapReady)
        {
            if (IsVersus)
            {
                SpawnCountersForTeam(instantiatedMapTeamA, 0);
                SpawnCountersForTeam(instantiatedMapTeamB, 1);
            }
            else
            {
                SpawnCountersForTeam(instantiatedMap, -1);
            }
            MapReady = true;

            if (IsVersus)
            {
                KitchenGameMultiplayer.Instance.SpawnAllPlayersVersus();
            }
            else
            {
                KitchenGameMultiplayer.Instance.SpawnAllPlayers();
            }

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

    private void SpawnCountersForTeam(GameObject mapInstance, int teamIndex)
    {
        if (mapInstance == null) return;

        CounterPlaceholder[] placeholders = mapInstance.GetComponentsInChildren<CounterPlaceholder>()
            .OrderBy(p => p.gameObject.name, StringComparer.Ordinal)
            .ToArray();

        if (placeholders.Length == 0) return;

        // Seeded RNG — running this pipeline twice (once per team) with the same seed produces
        // identical layouts, satisfying the "both kitchens must be identical" versus constraint.
        System.Random rng = new System.Random(RandomSeed);

        NetworkPrefabRef[] resolvedPrefabs = MapRandomizer.ResolveFlexChoices(placeholders, rng);

        if (!IsVersus)
        {
            for (int i = 0; i < placeholders.Length; i++)
            {
                if (placeholders[i].VersusOnly && !placeholders[i].IsFlex)
                {
                    resolvedPrefabs[i] = coopFallbackCounterPrefab;
                }
            }
        }

        MapRandomizer.ShufflePlaceholderPositions(placeholders, rng);

        int capturedTeamIndex = teamIndex;
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
                        counter.HasSpawnTransform = true;
                        counter.TeamIndex = capturedTeamIndex;
                    }
                }
            );
        }

        foreach (CounterPlaceholder placeholder in placeholders)
        {
            Destroy(placeholder.gameObject);
        }
    }
}
