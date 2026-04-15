using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class RecipeSO : ScriptableObject
{
    public List<KitchenObjectSO> kitchenObjectSOList;
    public string recipeName;
    [Tooltip("Seconds this order stays on the board before expiring. 0 or less = use VersusMatchManager fallback.")]
    public float orderExpiryDurationSeconds = 50f;
}
