using System.Collections.Generic;
using Fusion;
using UnityEngine;
using System;

public class DeliveryManager : NetworkBehaviour
{
    public class DeliveryEventArgs : EventArgs {
        public DeliveryCounter DeliveryCounter { get; }

        public DeliveryEventArgs(DeliveryCounter deliveryCounter) {
            DeliveryCounter = deliveryCounter;
        }
    }

    public event EventHandler OnRecipeSpawned;
    public event EventHandler OnRecipeCompleted;
    public event EventHandler<DeliveryEventArgs> OnRecipeSuccess;
    public event EventHandler<DeliveryEventArgs> OnRecipeFailed;

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

    public override void Despawned(NetworkRunner runner, bool hasState){
        if(Instance == this) Instance = null;
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
        if(!KitchenGameManager.IsReady) return;

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

    public void DeliverRecipe(DeliveryCounter deliveryCounter, PlateKitchenObject plateKitchenObject){
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

                    RPC_RecipeSuccess(deliveryCounter.GetNetworkObject().Id);
                    return;
                }
            }
        }
        RPC_RecipeFailed(deliveryCounter.GetNetworkObject().Id);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RecipeSuccess(NetworkId deliveryCounterId){
        OnRecipeSuccess?.Invoke(this, new DeliveryEventArgs(GetDeliveryCounterFromId(deliveryCounterId)));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RecipeFailed(NetworkId deliveryCounterId){
        OnRecipeFailed?.Invoke(this, new DeliveryEventArgs(GetDeliveryCounterFromId(deliveryCounterId)));
    }

    private DeliveryCounter GetDeliveryCounterFromId(NetworkId deliveryCounterId){
        if(Runner != null && Runner.TryFindObject(deliveryCounterId, out NetworkObject networkObject)){
            return networkObject.GetComponent<DeliveryCounter>();
        }

        return null;
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
