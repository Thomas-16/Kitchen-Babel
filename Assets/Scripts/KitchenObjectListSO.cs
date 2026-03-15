using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class KitchenObjectListSO : ScriptableObject
{
    public List<KitchenObjectSO> kitchenObjectSOList;

    public int GetIndex(KitchenObjectSO kitchenObjectSO){
        return kitchenObjectSOList.IndexOf(kitchenObjectSO);
    }
    public KitchenObjectSO GetByIndex(int index){
        return kitchenObjectSOList[index];
    }
}
