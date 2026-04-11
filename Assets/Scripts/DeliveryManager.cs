using System.Collections.Generic;
using Fusion;
using UnityEngine;
using System;

public class DeliveryManager : NetworkBehaviour
{
    public event EventHandler OnRecipeSpawned;
    public event EventHandler OnRecipeCompleted;
    public event EventHandler OnRecipeSuccess;
    public event EventHandler OnRecipeFailed;

    public static DeliveryManager Instance {get; private set;}
    public static event EventHandler OnInstanceReady;

    public static void ResetStaticData(){
        OnInstanceReady = null;
    }
    [SerializeField] private RecipeListSO recipeListSO;

    [Networked, Capacity(4)] public NetworkLinkedList<int> WaitingRecipeIndices => default;
    [Networked] public int SuccessfulRecipesAmount { get; set; }

    private float spawnRecipeTimer;
    private float spawnRecipeTimerMax = 4f;
    private int waitingRecipeMax = 4;
    private ChangeDetector changeDetector;
    private int lastKnownRecipeCount;

    public override void Spawned(){
        Instance = this;
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        lastKnownRecipeCount = WaitingRecipeIndices.Count;
        OnInstanceReady?.Invoke(this, EventArgs.Empty);
    }

    public override void Render(){
        int currentCount = WaitingRecipeIndices.Count;
        if(currentCount != lastKnownRecipeCount){
            if(currentCount > lastKnownRecipeCount){
                OnRecipeSpawned?.Invoke(this, EventArgs.Empty);
            }else{
                OnRecipeCompleted?.Invoke(this, EventArgs.Empty);
            }
            lastKnownRecipeCount = currentCount;
        }
    }

    public override void FixedUpdateNetwork(){
        if(!HasStateAuthority) return;

        if(KitchenGameManager.Instance.IsGamePlaying()){
            spawnRecipeTimer -= Runner.DeltaTime;
            if(spawnRecipeTimer <= 0f){
                spawnRecipeTimer = spawnRecipeTimerMax;

                if(WaitingRecipeIndices.Count < waitingRecipeMax){
                    int recipeIndex = UnityEngine.Random.Range(0, recipeListSO.recipeSOList.Count);
                    WaitingRecipeIndices.Add(recipeIndex);
                }
            }
        }
    }

    public void DeliverRecipe(PlateKitchenObject plateKitchenObject){
        for(int i = 0; i < WaitingRecipeIndices.Count; i++){
            RecipeSO waitingRecipeSO = recipeListSO.recipeSOList[WaitingRecipeIndices[i]];

            if(waitingRecipeSO.kitchenObjectSOList.Count == plateKitchenObject.GetKitchenObjectSOList().Count){
                bool plateContentsMatchesRecipe = true;
                foreach(KitchenObjectSO recipeKitchenObjectSO in waitingRecipeSO.kitchenObjectSOList){
                    bool ingredientFound = false;
                    foreach(KitchenObjectSO plateKitchenObjectSO in plateKitchenObject.GetKitchenObjectSOList()){
                        if(plateKitchenObjectSO == recipeKitchenObjectSO){
                            ingredientFound = true;
                            break;
                        }
                    }
                    if(!ingredientFound){
                        plateContentsMatchesRecipe = false;
                    }
                }
                if(plateContentsMatchesRecipe){
                    SuccessfulRecipesAmount++;
                    WaitingRecipeIndices.Remove(WaitingRecipeIndices[i]);

                    RPC_RecipeSuccess();
                    return;
                }
            }
        }
        RPC_RecipeFailed();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RecipeSuccess(){
        OnRecipeSuccess?.Invoke(this, EventArgs.Empty);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RecipeFailed(){
        OnRecipeFailed?.Invoke(this, EventArgs.Empty);
    }

    public List<RecipeSO> GetWaitingRecipeSOList(){
        List<RecipeSO> list = new List<RecipeSO>();
        foreach(int index in WaitingRecipeIndices){
            list.Add(recipeListSO.recipeSOList[index]);
        }
        return list;
    }
    public int GetSuccessfulRecipesAmount(){
        return SuccessfulRecipesAmount;
    }
}
