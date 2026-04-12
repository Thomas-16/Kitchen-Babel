using Fusion;
using UnityEngine;

public class KitchenObject : NetworkBehaviour
{
    [SerializeField] private KitchenObjectSO kitchenObjectSO;

    [Networked] public NetworkBehaviourId KitchenObjectParentId { get; set; }
    [Networked] public int KitchenObjectSOIndex { get; set; }

    private IKitchenObjectParent kitchenObjectParent;
    private ChangeDetector changeDetector;

    public override void Spawned(){
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // Resolve SO from index on all clients
        if(KitchenObjectSOIndex >= 0){
            kitchenObjectSO = KitchenGameMultiplayer.Instance.GetKitchenObjectListSO().GetByIndex(KitchenObjectSOIndex);
        }

        // Apply initial parent if set in onBeforeSpawned
        if(KitchenObjectParentId != default){
            ApplyParentVisual();
        }
    }

    public override void Render(){
        foreach(string propertyName in changeDetector.DetectChanges(this)){
            switch(propertyName){
                case nameof(KitchenObjectParentId):
                    ApplyParentVisual();
                    break;
            }
        }
    }

    private void ApplyParentVisual(){
        if(Runner.TryFindBehaviour(KitchenObjectParentId, out NetworkBehaviour parentBehaviour)){
            IKitchenObjectParent parent = parentBehaviour as IKitchenObjectParent;
            if(parent != null){
                // Clear old parent locally
                if(kitchenObjectParent != null){
                    kitchenObjectParent.ClearKitchenObeject();
                }

                kitchenObjectParent = parent;
                parent.SetKitchenObject(this);

                transform.parent = parent.GetKitchenObjectFollowTransform();
                transform.localPosition = Vector3.zero;
            }
        }
    }

    public KitchenObjectSO GetKitchenObjectSO(){
        return kitchenObjectSO;
    }

    public void SetKitchenObjectParent(IKitchenObjectParent kitchenObjectParent){
        if(this.kitchenObjectParent != null){
            this.kitchenObjectParent.ClearKitchenObeject();
        }
        this.kitchenObjectParent = kitchenObjectParent;

        if(kitchenObjectParent.HasKitchenObject()){
            Debug.LogError("KitchenObjectParent already has a KitchenObject!");
        }
        kitchenObjectParent.SetKitchenObject(this);

        // Set networked parent ID for syncing
        NetworkBehaviour parentNetworkBehaviour = kitchenObjectParent.GetNetworkObject().GetComponent<NetworkBehaviour>();
        // Try to get the specific IKitchenObjectParent NetworkBehaviour
        foreach(NetworkBehaviour nb in kitchenObjectParent.GetNetworkObject().GetComponentsInChildren<NetworkBehaviour>()){
            if(nb is IKitchenObjectParent){
                parentNetworkBehaviour = nb;
                break;
            }
        }
        KitchenObjectParentId = parentNetworkBehaviour.Id;

        transform.parent = kitchenObjectParent.GetKitchenObjectFollowTransform();
        transform.localPosition = Vector3.zero;
    }

    public IKitchenObjectParent GetKitchenObjectParent(){
        return this.kitchenObjectParent;
    }

    public void ClearKitchenObjectOnParent(){
        kitchenObjectParent.ClearKitchenObeject();
    }

    public void DestroySelf(){
        KitchenGameMultiplayer.Instance.DestroyKitchenObject(this);
    }

    public bool TryGetPlate(out PlateKitchenObject plateKitchenObject){
        if(this is PlateKitchenObject){
            plateKitchenObject = this as PlateKitchenObject;
            return true;
        }else{
            plateKitchenObject = null;
            return false;
        }
    }

    public static KitchenObject SpawnKitchenObject(KitchenObjectSO kitchenObjectSO, IKitchenObjectParent kitchenObjectParent){
        KitchenGameMultiplayer.Instance.SpawnKitchenObject(kitchenObjectSO, kitchenObjectParent);
        return null; // Object is spawned asynchronously via Fusion
    }
}
