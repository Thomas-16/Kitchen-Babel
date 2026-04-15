using System;
using Michsky.UI.Heat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI recipeNameText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Image iconImage;
    [SerializeField] private ButtonManager orderButton;
    [SerializeField] private GameObject unavailableOverlay;

    private int recipeIndex;
    private Action<int> onOrder;

    public void Setup(RecipeSO recipeSO, int recipeIndex, int cost, bool canAfford, Action<int> onOrder)
    {
        this.recipeIndex = recipeIndex;
        this.onOrder = onOrder;

        recipeNameText.text = recipeSO.recipeName;
        costText.text = $"${cost}";

        if (recipeSO.kitchenObjectSOList != null && recipeSO.kitchenObjectSOList.Count > 0)
        {
            iconImage.sprite = recipeSO.kitchenObjectSOList[0].sprite;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false;
        }

        SetAffordable(canAfford);
    }

    public void SetAffordable(bool canAfford)
    {
        unavailableOverlay.SetActive(!canAfford);
        orderButton.Interactable(canAfford);
    }

    public void HandleOrderClicked()
    {
        onOrder?.Invoke(recipeIndex);
    }
}
