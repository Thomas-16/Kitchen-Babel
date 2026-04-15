using Fusion;
using UnityEngine;
using System;

public class CuttingCounter : BaseCounter, IHasProgress
{
    public static event EventHandler OnAnyCut;

    new public static void ResetStaticData(){
        OnAnyCut = null;
    }

    public event EventHandler<IHasProgress.OnProgressChangedEventArgs> OnProgressChanged;

    public event EventHandler OnCut;

    [SerializeField] private CuttingRecipeSO[] cuttingRecipeSOArray;

    [Networked] public int CuttingProgress { get; set; }

    private ChangeDetector changeDetector;

    public override void Spawned(){
        base.Spawned();
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void Render(){
        foreach(string propertyName in changeDetector.DetectChanges(this)){
            switch(propertyName){
                case nameof(CuttingProgress):
                    OnCuttingProgressChanged();
                    break;
            }
        }
    }

    private void OnCuttingProgressChanged(){
        CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(
            HasKitchenObject() ? GetKitchenObject().GetKitchenObjectSO() : null
        );

        if(cuttingRecipeSO != null && CuttingProgress > 0){
            OnCut?.Invoke(this, EventArgs.Empty);
            OnAnyCut?.Invoke(this, EventArgs.Empty);
            OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs{
                progressNormalized = (float)CuttingProgress / cuttingRecipeSO.cuttingProgressMax
            });
        } else {
            // Recipe not found (item completed/removed) or progress reset — hide bar
            OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs{
                progressNormalized = 0f
            });
        }
    }

    public override void Interact(Player player)
    {
        if (!IsPlayerAllowed(player)) return;
        if(!HasKitchenObject()){
            if(player.HasKitchenObject()){
                if(HasRecipeWithInput(player.GetKitchenObject().GetKitchenObjectSO())){
                    player.GetKitchenObject().SetKitchenObjectParent(this);
                    CuttingProgress = 0;
                }
            }
        }else{
            if(player.HasKitchenObject()){
                if(player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject)){
                    if(plateKitchenObject.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO())){
                        GetKitchenObject().DestroySelf();
                    }
                }
            }else{
                GetKitchenObject().SetKitchenObjectParent(player);
                CuttingProgress = 0;
            }
        }
    }
    public override void InteractAlternate(Player player)
    {
        if (!IsPlayerAllowed(player)) return;
        if(HasKitchenObject() && HasRecipeWithInput(GetKitchenObject().GetKitchenObjectSO())){
            CuttingProgress++;

            CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());

            if(CuttingProgress >= cuttingRecipeSO.cuttingProgressMax){
                KitchenObjectSO outputKitchenObjectSO = GetOutputForInput(GetKitchenObject().GetKitchenObjectSO());
                GetKitchenObject().DestroySelf();

                KitchenObject.SpawnKitchenObject(outputKitchenObjectSO, this);
                CuttingProgress = 0;
            }
        }
    }
    private bool HasRecipeWithInput(KitchenObjectSO inputKitchenObjectSO) {
        CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(inputKitchenObjectSO);
        return cuttingRecipeSO != null;
    }

    private KitchenObjectSO GetOutputForInput(KitchenObjectSO inputKitchenObjectSO) {
        CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(inputKitchenObjectSO);
        if (cuttingRecipeSO != null) {
            return cuttingRecipeSO.output;
        } else {
            return null;
        }
    }

    private CuttingRecipeSO GetCuttingRecipeSOWithInput(KitchenObjectSO inputKitchenObjectSO) {
        if(inputKitchenObjectSO == null) return null;
        foreach (CuttingRecipeSO cuttingRecipeSO in cuttingRecipeSOArray) {
            if (cuttingRecipeSO.input == inputKitchenObjectSO) {
                return cuttingRecipeSO;
            }
        }
        return null;
    }
}
