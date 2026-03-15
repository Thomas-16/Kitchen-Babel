using Fusion;
using UnityEngine;
using System;

public class StoveCounter : BaseCounter, IHasProgress
{
    public event EventHandler<IHasProgress.OnProgressChangedEventArgs> OnProgressChanged;
    public event EventHandler<OnStageChangedEventArgs> OnStageChanged;
    public class OnStageChangedEventArgs : EventArgs{
        public State state;
    }

    public enum State{
        Idle,
        Frying,
        Fried,
        Burned,
    }
    [SerializeField] private FryingRecipeSO[] fryingRecipeSOArray;
    [SerializeField] private BurningRecipeSO[] burningRecipeSOArray;

    [Networked] public int NetworkedState { get; set; }
    [Networked] public float FryingTimer { get; set; }
    [Networked] public float BurningTimer { get; set; }

    private FryingRecipeSO fryingRecipeSO;
    private BurningRecipeSO burningRecipeSO;
    private ChangeDetector changeDetector;

    public override void Spawned(){
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        NetworkedState = (int)State.Idle;
    }

    public override void Render(){
        foreach(var propertyName in changeDetector.DetectChanges(this)){
            switch(propertyName){
                case nameof(NetworkedState):
                    OnStageChanged?.Invoke(this, new OnStageChangedEventArgs{
                        state = (State)NetworkedState
                    });
                    break;
                case nameof(FryingTimer):
                    if((State)NetworkedState == State.Frying && fryingRecipeSO != null){
                        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs{
                            progressNormalized = FryingTimer / fryingRecipeSO.fryingTimerMax
                        });
                    }
                    break;
                case nameof(BurningTimer):
                    if((State)NetworkedState == State.Fried && burningRecipeSO != null){
                        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs{
                            progressNormalized = BurningTimer / burningRecipeSO.burningTimerMax
                        });
                    }
                    break;
            }
        }
    }

    public override void FixedUpdateNetwork(){
        if(!HasStateAuthority) return;
        if(KitchenGameManager.Instance.IsPaused) return;

        if(HasKitchenObject()){
            State currentState = (State)NetworkedState;
            switch(currentState){
                case State.Idle:
                    break;
                case State.Frying:
                    FryingTimer += Runner.DeltaTime;

                    if(FryingTimer > fryingRecipeSO.fryingTimerMax){
                        GetKitchenObject().DestroySelf();

                        KitchenObject.SpawnKitchenObject(fryingRecipeSO.output, this);

                        burningRecipeSO = GetBurningRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());
                        NetworkedState = (int)State.Fried;
                        BurningTimer = 0f;
                    }
                    break;
                case State.Fried:
                    BurningTimer += Runner.DeltaTime;

                    if(BurningTimer > burningRecipeSO.burningTimerMax){
                        GetKitchenObject().DestroySelf();

                        KitchenObject.SpawnKitchenObject(burningRecipeSO.output, this);

                        NetworkedState = (int)State.Burned;
                    }
                    break;
                case State.Burned:
                    break;
            }
        }
    }

    public override void Interact(Player player)
    {
        if(!HasKitchenObject()){
            if(player.HasKitchenObject()){
                if(HasRecipeWithInput(player.GetKitchenObject().GetKitchenObjectSO())){
                    player.GetKitchenObject().SetKitchenObjectParent(this);

                    fryingRecipeSO = GetFryingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());

                    NetworkedState = (int)State.Frying;
                    FryingTimer = 0f;
                }
            }
        }else{
            if(player.HasKitchenObject()){
                if(player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject)){
                    if(plateKitchenObject.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO())){
                        GetKitchenObject().DestroySelf();

                        NetworkedState = (int)State.Idle;
                    }
                }
            }else{
                GetKitchenObject().SetKitchenObjectParent(player);

                NetworkedState = (int)State.Idle;
            }
        }
    }

    private bool HasRecipeWithInput(KitchenObjectSO inputKitchenObjectSO){
        FryingRecipeSO fryingRecipeSO = GetFryingRecipeSOWithInput(inputKitchenObjectSO);
        return fryingRecipeSO != null;
    }
    private KitchenObjectSO GetOutputForInput(KitchenObjectSO inputKitchenObjectSO){
        FryingRecipeSO fryingRecipeSO = GetFryingRecipeSOWithInput(inputKitchenObjectSO);
        if(fryingRecipeSO != null){
            return fryingRecipeSO.output;
        }else{
            return null;
        }
    }
    private FryingRecipeSO GetFryingRecipeSOWithInput(KitchenObjectSO inputKitchenObjectSO){
        foreach(FryingRecipeSO fryingRecipeSO in fryingRecipeSOArray){
            if(fryingRecipeSO.input == inputKitchenObjectSO){
                return fryingRecipeSO;
            }
        }
        return null;
    }
    private BurningRecipeSO GetBurningRecipeSOWithInput(KitchenObjectSO inputKitchenObjectSO){
        foreach(BurningRecipeSO burningRecipeSO in burningRecipeSOArray){
            if(burningRecipeSO.input == inputKitchenObjectSO){
                return burningRecipeSO;
            }
        }
        return null;
    }
    public bool isFried(){
        return (State)NetworkedState == State.Fried;
    }
}
