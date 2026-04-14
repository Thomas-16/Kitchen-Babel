using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DeliveryManagerSingleUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI recipeNameText;
    [SerializeField] private Transform iconContainer;
    [SerializeField] private Transform iconTemplate;
    [SerializeField] private Image[] countdownBars;
    [SerializeField] private Color sabotageColor = Color.red;

    private NetworkRunner runner;
    private int expirationTick;
    private int totalDurationTicks;
    private bool timerActive;

    private void Awake() {
        iconTemplate.gameObject.SetActive(false);
    }
    public void SetRecipeSO(RecipeSO recipeSO){
        recipeNameText.text = recipeSO.recipeName;

        foreach(Transform child in iconContainer){
            if(child == iconTemplate) continue;
            Destroy(child.gameObject);
        }

        foreach(KitchenObjectSO kitchenObjectSO in recipeSO.kitchenObjectSOList){
            Transform iconTransform = Instantiate(iconTemplate, iconContainer);
            iconTransform.gameObject.SetActive(true);
            iconTransform.GetComponent<Image>().sprite = kitchenObjectSO.sprite;
        }
    }

    public void SetOrder(RecipeSO recipeSO, int expirationTick, int totalDurationTicks, bool isSabotage, NetworkRunner runner) {
        SetRecipeSO(recipeSO);

        this.runner = runner;
        this.expirationTick = expirationTick;
        this.totalDurationTicks = Mathf.Max(1, totalDurationTicks);
        this.timerActive = true;

        if (isSabotage) {
            recipeNameText.color = sabotageColor;
            if (countdownBars != null) {
                foreach (Image bar in countdownBars) {
                    if (bar != null) bar.color = sabotageColor;
                }
            }
        }

        UpdateCountdownFill();
    }

    private void Update() {
        if (!timerActive || runner == null) return;
        UpdateCountdownFill();
    }

    private void UpdateCountdownFill() {
        if (countdownBars == null || countdownBars.Length == 0) return;
        float remainingTicks = expirationTick - runner.Tick;
        float fill = Mathf.Clamp01(remainingTicks / (float)totalDurationTicks);
        foreach (Image bar in countdownBars) {
            if (bar != null) bar.fillAmount = fill;
        }
    }
}
