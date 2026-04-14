using UnityEngine;
using TMPro;
using System;

public class CoopGameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI recipesDeliveredText;

    private void Start() {
        KitchenGameManager.OnInstanceReady += KitchenGameManager_OnInstanceReady;
        if(KitchenGameManager.Instance != null){
            KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;
        }
        Hide();
    }
    private void KitchenGameManager_OnInstanceReady(object sender, EventArgs e){
        KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;
    }
    private void KitchenGameManager_OnStateChanged(object sender, EventArgs e){
        if(MapManager.Instance != null && MapManager.Instance.IsVersus){
            Hide();
            return;
        }
        if(KitchenGameManager.Instance.IsGameOver()){
            Show();
            recipesDeliveredText.text = DeliveryManager.Instance.GetSuccessfulRecipesAmount().ToString();
        }else{
            Hide();
        }
    }
    private void Show(){
        gameObject.SetActive(true);
    }
    private void Hide(){
        gameObject.SetActive(false);
    }
}
