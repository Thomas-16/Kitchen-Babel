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

    private void Awake() {
        animator = GetComponent<Animator>();
    }
    private void Start() {
        DeliveryManager.OnInstanceReady += DeliveryManager_OnInstanceReady;
        if(DeliveryManager.Instance != null){
            SubscribeToManager();
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
    private void DeliveryManager_OnRecipeFailed(object sender, EventArgs e){
        gameObject.SetActive(true);
        animator.SetTrigger(POPUP);
        backgroundImage.color = failedColor;
        iconImage.sprite = failedSprite;
        messageText.text = "DELIVERY\nFAILED";
    }
    private void DeliveryManager_OnRecipeSucess(object sender, EventArgs e){
        gameObject.SetActive(true);
        animator.SetTrigger(POPUP);
        backgroundImage.color = successColor;
        iconImage.sprite = successSprite;
        messageText.text = "DELIVERY\nSUCCESS";
    }
}
