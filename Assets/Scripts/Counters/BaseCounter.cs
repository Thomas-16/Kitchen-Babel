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

    public override void Spawned()
    {
        // Clients don't have the spawn position from Runner.Spawn() — apply from networked state
        if (!HasStateAuthority)
        {
            transform.position = SpawnPosition;
            transform.rotation = SpawnRotation;
        }
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
