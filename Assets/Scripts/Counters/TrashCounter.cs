using UnityEngine;
using System;

public class TrashCounter : BaseCounter
{
    public static event EventHandler OnAnyObjectTrashed;

    new public static void ResetStaticData(){
        OnAnyObjectTrashed = null;
    }

    public override void Interact(Player player)
    {
        if (!IsPlayerAllowed(player)) return;
        if(player.HasKitchenObject()){
            player.GetKitchenObject().DestroySelf();

            OnAnyObjectTrashed?.Invoke(this, EventArgs.Empty);
        }
    }
}
