using System;
using Michsky.UI.Heat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI recipeNameText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Image[] iconImages;
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

        var ingredients = recipeSO.kitchenObjectSOList;
        int ingredientCount = ingredients != null ? ingredients.Count : 0;
        for (int i = 0; i < iconImages.Length; i++)
        {
            Image icon = iconImages[i];
            if (i < ingredientCount)
            {
                icon.sprite = ingredients[i].sprite;
                icon.enabled = true;
                icon.gameObject.SetActive(true);
            }
            else
            {
                icon.gameObject.SetActive(false);
            }
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
