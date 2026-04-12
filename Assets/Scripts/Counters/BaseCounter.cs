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

    private KitchenObject kitchenObject;
    private bool hasAppliedSpawnTransform;

    public override void Spawned()
    {
        // Apply networked position immediately on all peers so host-migrated counters
        // (where the new host has state authority but the Runner.Spawn position was 0,0,0
        // because the ghost had no NetworkTransform) land at the correct transform.
        TryApplySpawnTransform();
    }

    private void LateUpdate()
    {
        // Safety net for clients and for any frame where Spawned() ran before CopyStateFrom
        // populated SpawnPosition (shouldn't happen with onBeforeSpawned, but cheap to keep).
        if (!hasAppliedSpawnTransform)
        {
            TryApplySpawnTransform();
        }
    }

    private void TryApplySpawnTransform()
    {
        if (hasAppliedSpawnTransform) return;
        if (SpawnPosition == Vector3.zero && SpawnRotation == Quaternion.identity) return;

        transform.position = SpawnPosition;
        transform.rotation = SpawnRotation;
        hasAppliedSpawnTransform = true;
    }

    public virtual void Interact(Player player){

    }
    public virtual void InteractAlternate(Player player){

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
