using UnityEngine;

public class DeliveryCounter : BaseCounter
{
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
