using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class KitchenGameMultiplayer : NetworkBehaviour, INetworkRunnerCallbacks
{
    public static KitchenGameMultiplayer Instance { get; private set; }
    public static event Action OnInstanceReady;

    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private KitchenObjectListSO kitchenObjectListSO;

    private List<Transform> spawnPositions = new List<Transform>();
    private List<Transform> spawnPositionsTeamA = new List<Transform>();
    private List<Transform> spawnPositionsTeamB = new List<Transform>();

    private static bool isHostMigrationResume;

    public static void SetHostMigrationFlag()
    {
        isHostMigrationResume = true;
    }

    public override void Spawned(){
        Instance = this;
        Runner.AddCallbacks(this);

        // During host migration, player objects are restored from the snapshot — don't spawn duplicates
        if (isHostMigrationResume)
        {
            Debug.Log("[KGM] Spawned() during host migration — skipping player spawning.");
            isHostMigrationResume = false;
        }
        else
        {
            // Player spawning is deferred until MapManager calls SpawnAllPlayers()
            Debug.Log($"[KGM] Spawned() called. IsServer={Runner.IsServer}, ActivePlayers count={Runner.ActivePlayers.Count()}");
        }

        // Always fire so MapManager can proceed with map setup
        OnInstanceReady?.Invoke();
    }

    public void SetSpawnPositions(List<Transform> positions)
    {
        spawnPositions = positions;
    }

    public void SetSpawnPositionsVersus(List<Transform> teamA, List<Transform> teamB)
    {
        spawnPositionsTeamA = teamA;
        spawnPositionsTeamB = teamB;
    }

    public void SpawnAllPlayers()
    {
        if (!Runner.IsServer) return;
        if (spawnPositions.Count == 0)
        {
            Debug.LogWarning("[KGM] SpawnAllPlayers called but no spawn positions set!");
            return;
        }

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            int spawnIndex = player.AsIndex % spawnPositions.Count;
            Transform spawnPoint = spawnPositions[spawnIndex];
            Debug.Log($"[KGM] Spawning player prefab for {player} at index {spawnIndex}, pos={spawnPoint.position}");
            PlayerRef capturedPlayer = player;
            Runner.Spawn(playerPrefab, spawnPoint.position, spawnPoint.rotation, player,
                (runner, obj) =>
                {
                    Player p = obj.GetComponent<Player>();
                    p.ColorIndex = LobbyManager.GetCachedPlayerColorIndex(capturedPlayer);
                    p.PlayerName = LobbyManager.GetCachedPlayerName(capturedPlayer);
                }
            );
        }
    }

    public void SpawnAllPlayersVersus()
    {
        if (!Runner.IsServer) return;
        if (spawnPositionsTeamA.Count == 0 || spawnPositionsTeamB.Count == 0)
        {
            Debug.LogWarning("[KGM] SpawnAllPlayersVersus called but per-team spawn positions are not set!");
            return;
        }

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            SpawnVersusPlayer(player);
        }
    }

    private void SpawnVersusPlayer(PlayerRef player)
    {
        int teamIndex = LobbyManager.GetCachedPlayerTeamIndex(player);
        if (teamIndex != 0 && teamIndex != 1)
        {
            Debug.LogWarning($"[KGM] Versus spawn for {player} has unresolved team ({teamIndex}); defaulting to Team A.");
            teamIndex = 0;
        }

        List<Transform> teamSpawns = teamIndex == 1 ? spawnPositionsTeamB : spawnPositionsTeamA;
        if (teamSpawns.Count == 0) return;

        int spawnIndex = GetVersusTeamSlotIndex(player, teamIndex, teamSpawns.Count);
        Transform spawnPoint = teamSpawns[spawnIndex];
        Debug.Log($"[KGM] Versus spawning {player} on team {teamIndex} at index {spawnIndex}, pos={spawnPoint.position}");

        PlayerRef capturedPlayer = player;
        int capturedTeamIndex = teamIndex;
        Runner.Spawn(playerPrefab, spawnPoint.position, spawnPoint.rotation, player,
            (runner, obj) =>
            {
                Player p = obj.GetComponent<Player>();
                p.ColorIndex = LobbyManager.GetCachedPlayerColorIndex(capturedPlayer);
                p.PlayerName = LobbyManager.GetCachedPlayerName(capturedPlayer);
                p.TeamIndex = capturedTeamIndex;
            }
        );
    }

    // Count same-team players with a lower PlayerRef index so each team member gets a unique
    // spawn slot within their own kitchen. `player.AsIndex` alone is the global lobby index
    // and collides (e.g. Team A holding refs 0 and 2 both resolve to slot 0 with 2 spawns).
    private int GetVersusTeamSlotIndex(PlayerRef player, int teamIndex, int slotCount)
    {
        if (slotCount <= 0) return 0;
        int position = 0;
        foreach (PlayerRef other in Runner.ActivePlayers)
        {
            if (other == player) continue;
            if (other.AsIndex >= player.AsIndex) continue;
            if (LobbyManager.GetCachedPlayerTeamIndex(other) != teamIndex) continue;
            position++;
        }
        return position % slotCount;
    }

    public override void Despawned(NetworkRunner runner, bool hasState){
        Runner.RemoveCallbacks(this);
        if(Instance == this) Instance = null;
    }

    public KitchenObjectListSO GetKitchenObjectListSO(){
        return kitchenObjectListSO;
    }

    public void SpawnKitchenObject(KitchenObjectSO kitchenObjectSO, IKitchenObjectParent kitchenObjectParent){
        int kitchenObjectSOIndex = kitchenObjectListSO.GetIndex(kitchenObjectSO);

        NetworkObject kitchenObjectParentNetworkObject = kitchenObjectParent.GetNetworkObject();

        Runner.Spawn(kitchenObjectSO.prefab.GetComponent<NetworkObject>(), Vector3.zero, Quaternion.identity, Object.InputAuthority,
            (runner, obj) => {
                KitchenObject kitchenObject = obj.GetComponent<KitchenObject>();
                kitchenObject.KitchenObjectSOIndex = kitchenObjectSOIndex;
                kitchenObject.SetKitchenObjectParent(kitchenObjectParent);
            }
        );
    }

    public void DestroyKitchenObject(KitchenObject kitchenObject){
        kitchenObject.ClearKitchenObjectOnParent();
        Runner.Despawn(kitchenObject.Object);
    }

    // INetworkRunnerCallbacks
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player){
        Debug.Log($"[KGM] OnPlayerJoined: {player}, IsServer={runner.IsServer}");
        if(!runner.IsServer) return;

        // After host migration Fusion re-fires OnPlayerJoined for every connected player,
        // but their Player NetworkObject was already restored by HostMigrationResume.
        // Skip the spawn if a Player already exists for this PlayerRef.
        foreach (NetworkObject existing in runner.GetAllNetworkObjects())
        {
            if (existing.InputAuthority == player && existing.GetComponent<Player>() != null)
            {
                Debug.Log($"[KGM] Player already exists for {player} — skipping spawn.");
                return;
            }
        }

        bool isVersus = LobbyManager.GetCachedMatchMode() == LobbyManager.MatchModeVersus;
        if (isVersus)
        {
            if (spawnPositionsTeamA.Count == 0 || spawnPositionsTeamB.Count == 0)
            {
                Debug.LogWarning("[KGM] OnPlayerJoined (versus) but per-team spawn positions are not set yet — skipping spawn.");
                return;
            }
            SpawnVersusPlayer(player);
            return;
        }

        if (spawnPositions.Count == 0)
        {
            Debug.LogWarning("[KGM] OnPlayerJoined but no spawn positions set yet — skipping spawn.");
            return;
        }

        int spawnIndex = player.AsIndex % spawnPositions.Count;
        Transform spawnPoint = spawnPositions[spawnIndex];

        NetworkObject playerNetworkObject = runner.Spawn(playerPrefab, spawnPoint.position, spawnPoint.rotation, player,
            (runner, obj) =>
            {
                Player p = obj.GetComponent<Player>();
                p.ColorIndex = LobbyManager.GetCachedPlayerColorIndex(player);
                p.PlayerName = LobbyManager.GetCachedPlayerName(player);
            }
        );
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player){
        if (!runner.IsServer) return;
        Debug.Log($"[KGM] OnPlayerLeft: {player}");

        // Despawn any kitchen object held by the leaving player before Fusion auto-despawns them
        foreach (NetworkObject networkObject in runner.GetAllNetworkObjects())
        {
            if (networkObject.InputAuthority != player) continue;

            Player playerComponent = networkObject.GetComponent<Player>();
            if (playerComponent != null && playerComponent.HasKitchenObject())
            {
                KitchenObject heldObject = playerComponent.GetKitchenObject();
                if (heldObject != null && heldObject.Object != null && heldObject.Object.IsValid)
                {
                    Debug.Log($"[KGM] Despawning kitchen object held by leaving player {player}");
                    runner.Despawn(heldObject.Object);
                }
            }
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input){
        PlayerNetworkInput playerInput = new PlayerNetworkInput();
        playerInput.moveDirection = OrderSabotageModalUI.IsLocalOpen
            ? Vector2.zero
            : GameInput.Instance.GetMovementVectorNormalized();

        if(GameInput.Instance.GetInteractPressed()){
            playerInput.buttons.Set(InputButton.Interact, true);
        }
        if(GameInput.Instance.GetInteractAlternatePressed()){
            playerInput.buttons.Set(InputButton.InteractAlternate, true);
        }
        if(GameInput.Instance.GetDashPressed()){
            playerInput.buttons.Set(InputButton.Dash, true);
        }

        if(Player.LocalInstance != null){
            playerInput.position = Player.LocalInstance.transform.position;
            playerInput.rotation = Player.LocalInstance.transform.rotation;
        }

        input.Set(playerInput);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input){ }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason){ }
    public void OnConnectedToServer(NetworkRunner runner){ }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason){ }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token){ }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason){ }
    public void OnSceneLoadStart(NetworkRunner runner){ }
    public void OnSceneLoadDone(NetworkRunner runner){ }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList){ }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data){ }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken){ }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data){ }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress){ }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message){ }
}
