using UnityEngine;

public class ResetStaticDataManager : MonoBehaviour
{
    private void Awake() {
        CuttingCounter.ResetStaticData();
        BaseCounter.ResetStaticData();
        TrashCounter.ResetStaticData();
        DeliveryCounter.ResetStaticData();
        Player.ResetStaticData();
        KitchenGameManager.ResetStaticData();
        DeliveryManager.ResetStaticData();
        VersusMatchManager.ResetStaticData();
        OrderSabotageModalUI.ResetStaticData();
    }
}
