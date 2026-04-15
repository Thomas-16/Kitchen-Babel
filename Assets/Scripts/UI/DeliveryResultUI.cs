using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class DeliveryResultUI : MonoBehaviour
{
    private const string POPUP = "Popup";

    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Color successColor;
    [SerializeField] private Color failedColor;
    [SerializeField] private Sprite successSprite;
    [SerializeField] private Sprite failedSprite;

    private Animator animator;
    private DeliveryCounter deliveryCounter;

    private void Awake() {
        animator = GetComponent<Animator>();
        deliveryCounter = GetComponentInParent<DeliveryCounter>();
    }
    private void Start() {
        DeliveryManager.OnInstanceReady += DeliveryManager_OnInstanceReady;
        if(DeliveryManager.Instance != null){
            SubscribeToManager();
        }
        VersusMatchManager.OnInstanceReady += VersusMatchManager_OnInstanceReady;
        if(VersusMatchManager.IsReady){
            SubscribeToVersusManager();
        }
        gameObject.SetActive(false);
    }
    private void DeliveryManager_OnInstanceReady(object sender, EventArgs e){
        SubscribeToManager();
    }
    private void SubscribeToManager(){
        DeliveryManager.Instance.OnRecipeSuccess += DeliveryManager_OnRecipeSucess;
        DeliveryManager.Instance.OnRecipeFailed += DeliveryManager_OnRecipeFailed;
    }
    private void VersusMatchManager_OnInstanceReady(object sender, EventArgs e){
        SubscribeToVersusManager();
    }
    private void SubscribeToVersusManager(){
        VersusMatchManager.Instance.OnDeliveryAttempted += VersusMatchManager_OnDeliveryAttempted;
    }
    private void VersusMatchManager_OnDeliveryAttempted(object sender, VersusMatchManager.DeliveryAttemptEventArgs e){
        if(deliveryCounter == null || e.TeamIndex != deliveryCounter.TeamIndex) return;

        gameObject.SetActive(true);
        animator.SetTrigger(POPUP);
        if(e.Success){
            backgroundImage.color = successColor;
            iconImage.sprite = successSprite;
            messageText.text = "DELIVERY\nSUCCESS";
        }else{
            backgroundImage.color = failedColor;
            iconImage.sprite = failedSprite;
            messageText.text = "DELIVERY\nFAILED";
        }
    }
    private void DeliveryManager_OnRecipeFailed(object sender, DeliveryManager.DeliveryEventArgs e){
        if(e.DeliveryCounter != deliveryCounter) return;

        gameObject.SetActive(true);
        animator.SetTrigger(POPUP);
        backgroundImage.color = failedColor;
        iconImage.sprite = failedSprite;
        messageText.text = "DELIVERY\nFAILED";
    }
    private void DeliveryManager_OnRecipeSucess(object sender, DeliveryManager.DeliveryEventArgs e){
        if(e.DeliveryCounter != deliveryCounter) return;

        gameObject.SetActive(true);
        animator.SetTrigger(POPUP);
        backgroundImage.color = successColor;
        iconImage.sprite = successSprite;
        messageText.text = "DELIVERY\nSUCCESS";
    }
}
