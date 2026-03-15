using Fusion;
using UnityEngine;
using System;

public class ContainerCounter : BaseCounter
{
    public event EventHandler OnPlayerGrabbedObject;

    [SerializeField] private KitchenObjectSO kitchenObjectSO;

    public override void Interact(Player player){
        if(!player.HasKitchenObject()){
            KitchenObject.SpawnKitchenObject(kitchenObjectSO, player);

            RPC_OnPlayerGrabbedObject();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerGrabbedObject(){
        OnPlayerGrabbedObject?.Invoke(this, EventArgs.Empty);
    }
}
