using Fusion;
using UnityEngine;
using System;

public class BaseCounter : NetworkBehaviour, IKitchenObjectParent
{
    public static event EventHandler OnAnyObjectPlacedHere;

    public static void ResetStaticData(){
        OnAnyObjectPlacedHere = null;
    }

    [SerializeField] private Transform counterTopPoint;

    [Networked] public Vector3 SpawnPosition { get; set; }
    [Networked] public Quaternion SpawnRotation { get; set; }
    [Networked] public NetworkBool HasSpawnTransform { get; set; }
    [Networked] public int TeamIndex { get; set; } = -1;

    private KitchenObject kitchenObject;
    private bool hasAppliedSpawnTransform;

    private void LateUpdate()
    {
        if (hasAppliedSpawnTransform) return;
        if (HasStateAuthority) return;
        // Gate on an explicit networked flag rather than comparing against
        // (Vector3.zero, Quaternion.identity) — a Team A counter legitimately
        // placed at world origin would otherwise never apply its spawn transform.
        if (!HasSpawnTransform) return;

        transform.position = SpawnPosition;
        transform.rotation = SpawnRotation;
        hasAppliedSpawnTransform = true;
    }

    public virtual void Interact(Player player){

    }
    public virtual void InteractAlternate(Player player){

    }

    // Defence-in-depth team check. The raycast gate in Player.HandleCounterSelection
    // is the primary enforcement, but this lets subclasses reject cross-team
    // interactions at the counter level too — including during the brief window
    // after a client spawns where Player.TeamIndex hasn't replicated yet.
    protected bool IsPlayerAllowed(Player player){
        if (player == null) return false;
        if (TeamIndex == -1) return true;        // co-op counter — anyone can use
        if (player.TeamIndex == -1) return false; // player team not yet synced
        return player.TeamIndex == TeamIndex;
    }

    public Transform GetKitchenObjectFollowTransform(){
        return counterTopPoint;
    }
    public void SetKitchenObject(KitchenObject kitchenObject){
        this.kitchenObject = kitchenObject;

        if(kitchenObject != null){
            OnAnyObjectPlacedHere?.Invoke(this ,EventArgs.Empty);
        }
    }
    public KitchenObject GetKitchenObject(){
        return this.kitchenObject;
    }
    public void ClearKitchenObeject(){
        this.kitchenObject = null;
    }
    public bool HasKitchenObject(){
        return kitchenObject != null;
    }
    public NetworkObject GetNetworkObject(){
        return Object;
    }
}
