using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class DeliveryCounter : BaseCounter
{
    private static readonly Dictionary<int, DeliveryCounter> countersByTeam = new Dictionary<int, DeliveryCounter>();

    public static DeliveryCounter GetForTeam(int teamIndex)
    {
        countersByTeam.TryGetValue(teamIndex, out DeliveryCounter counter);
        return counter;
    }

    public override void Spawned()
    {
        base.Spawned();
        if (TeamIndex >= 0)
        {
            countersByTeam[TeamIndex] = this;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (TeamIndex >= 0 && countersByTeam.TryGetValue(TeamIndex, out DeliveryCounter existing) && existing == this)
        {
            countersByTeam.Remove(TeamIndex);
        }
        base.Despawned(runner, hasState);
    }

    public override void Interact(Player player)
    {
        if(player.HasKitchenObject()){
            if(player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject)){
                if(TeamIndex == -1){
                    DeliveryManager.Instance.DeliverRecipe(this, plateKitchenObject);
                }else if(VersusMatchManager.IsReady){
                    VersusMatchManager.Instance.DeliverRecipe(TeamIndex, plateKitchenObject);
                }

                player.GetKitchenObject().DestroySelf();
            }
        }
    }
}
