using UnityEngine;

public class DeliveryCounter : BaseCounter
{
    public static DeliveryCounter Instance {get; private set;}

    public override void Spawned() {
        Instance = this;
    }
    public override void Interact(Player player)
    {
        if(player.HasKitchenObject()){
            if(player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject)){
                DeliveryManager.Instance.DeliverRecipe(plateKitchenObject);

                player.GetKitchenObject().DestroySelf();
            }
        }
    }
}
