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
    private float[] barLengths;
    private float[] cumulativeLengthBefore;
    private float totalBarLength;

    private void Awake() {
        iconTemplate.gameObject.SetActive(false);
        CacheBarLengths();
    }

    private void CacheBarLengths() {
        if (countdownBars == null || countdownBars.Length == 0) return;
        barLengths = new float[countdownBars.Length];
        cumulativeLengthBefore = new float[countdownBars.Length];
        totalBarLength = 0f;
        for (int i = 0; i < countdownBars.Length; i++) {
            Image bar = countdownBars[i];
            float length = 0f;
            if (bar != null) {
                Rect rect = bar.rectTransform.rect;
                switch (bar.fillMethod) {
                    case Image.FillMethod.Horizontal: length = rect.width; break;
                    case Image.FillMethod.Vertical: length = rect.height; break;
                    default: length = Mathf.Max(rect.width, rect.height); break;
                }
            }
            barLengths[i] = length;
            cumulativeLengthBefore[i] = totalBarLength;
            totalBarLength += length;
        }
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
        if (barLengths == null || barLengths.Length != countdownBars.Length) CacheBarLengths();
        if (totalBarLength <= 0f) return;

        float remainingTicks = expirationTick - runner.Tick;
        float remainingFraction = Mathf.Clamp01(remainingTicks / (float)totalDurationTicks);
        float remainingLength = remainingFraction * totalBarLength;

        for (int i = 0; i < countdownBars.Length; i++) {
            Image bar = countdownBars[i];
            if (bar == null) continue;
            float barLength = barLengths[i];
            if (barLength <= 0f) { bar.fillAmount = 0f; continue; }
            float fillLen = Mathf.Clamp(remainingLength - cumulativeLengthBefore[i], 0f, barLength);
            bar.fillAmount = fillLen / barLength;
        }
    }
}
