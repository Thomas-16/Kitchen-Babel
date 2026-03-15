using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class KitchenGameMultiplayer : NetworkBehaviour, INetworkRunnerCallbacks
{
    public static KitchenGameMultiplayer Instance { get; private set; }

    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private List<Transform> spawnPositions;
    [SerializeField] private KitchenObjectListSO kitchenObjectListSO;

    public override void Spawned(){
        Instance = this;
        Runner.AddCallbacks(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState){
        Runner.RemoveCallbacks(this);
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
        if(runner.IsServer){
            int spawnIndex = player.AsIndex % spawnPositions.Count;
            Transform spawnPoint = spawnPositions[spawnIndex];

            NetworkObject playerNetworkObject = runner.Spawn(playerPrefab, spawnPoint.position, spawnPoint.rotation, player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player){
        // Fusion handles despawning player objects automatically
    }

    public void OnInput(NetworkRunner runner, NetworkInput input){
        PlayerNetworkInput playerInput = new PlayerNetworkInput();
        playerInput.moveDirection = GameInput.Instance.GetMovementVectorNormalized();

        if(GameInput.Instance.GetInteractPressed()){
            playerInput.buttons.Set(InputButton.Interact, true);
        }
        if(GameInput.Instance.GetInteractAlternatePressed()){
            playerInput.buttons.Set(InputButton.InteractAlternate, true);
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
