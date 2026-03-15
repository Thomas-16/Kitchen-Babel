using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlateKitchenObject : KitchenObject
{
    public event EventHandler<OnIngredientAddedEventArgs> OnIngredientAdded;
    public class OnIngredientAddedEventArgs : EventArgs{
        public KitchenObjectSO kitchenObjectSO;
    }

    [SerializeField] private List<KitchenObjectSO> validKitchenObjectSOList;

    [Networked, Capacity(8)] public NetworkLinkedList<int> IngredientSOIndices => default;

    private int lastKnownIngredientCount;

    public override void Spawned(){
        base.Spawned();
        lastKnownIngredientCount = 0;
    }

    public override void Render(){
        base.Render();

        // Detect ingredient list changes
        int currentCount = IngredientSOIndices.Count;
        if(currentCount > lastKnownIngredientCount){
            KitchenObjectListSO listSO = KitchenGameMultiplayer.Instance.GetKitchenObjectListSO();
            for(int i = lastKnownIngredientCount; i < currentCount; i++){
                KitchenObjectSO addedSO = listSO.GetByIndex(IngredientSOIndices[i]);
                OnIngredientAdded?.Invoke(this, new OnIngredientAddedEventArgs{
                    kitchenObjectSO = addedSO
                });
            }
            lastKnownIngredientCount = currentCount;
        }
    }

    public bool TryAddIngredient(KitchenObjectSO kitchenObjectSO){
        if(!validKitchenObjectSOList.Contains(kitchenObjectSO)){
            return false;
        }
        // Check for duplicate
        int index = KitchenGameMultiplayer.Instance.GetKitchenObjectListSO().GetIndex(kitchenObjectSO);
        foreach(int existingIndex in IngredientSOIndices){
            if(existingIndex == index){
                return false;
            }
        }
        IngredientSOIndices.Add(index);
        return true;
    }

    public List<KitchenObjectSO> GetKitchenObjectSOList(){
        List<KitchenObjectSO> list = new List<KitchenObjectSO>();
        KitchenObjectListSO listSO = KitchenGameMultiplayer.Instance.GetKitchenObjectListSO();
        foreach(int index in IngredientSOIndices){
            list.Add(listSO.GetByIndex(index));
        }
        return list;
    }
}
