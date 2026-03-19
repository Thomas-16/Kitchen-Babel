using Fusion;
using UnityEngine;
using System;

public class Player : NetworkBehaviour, IKitchenObjectParent
{
    public static Player LocalInstance { get; private set; }

    public static event EventHandler OnAnyPlayerSpawned;
    public static event EventHandler OnAnyPickedSomething;

    public event EventHandler OnPickedSomething;
    public event EventHandler<OnSelectedCounterChangedEventArgs> OnSelectedCounterChanged;
    public class OnSelectedCounterChangedEventArgs : EventArgs{
        public BaseCounter selectedCounter;
    }

    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private LayerMask countersLayerMask;
    [SerializeField] private Transform kitchenObjectHoldPoint;

    [SerializeField] private CharacterController controller;

    [Networked] public NetworkBool IsWalking { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private Vector3 lastInteractDir;
    private BaseCounter selectedCounter;
    private KitchenObject kitchenObject;

    public static void ResetStaticData(){
        OnAnyPlayerSpawned = null;
        OnAnyPickedSomething = null;
    }

    public override void Spawned(){
        if(HasInputAuthority){
            LocalInstance = this;
            OnAnyPlayerSpawned?.Invoke(this, EventArgs.Empty);
        }
    }

    public override void FixedUpdateNetwork(){
        if(KitchenGameManager.Instance == null) return;
        if(KitchenGameManager.Instance.IsPaused) return;

        if(!KitchenGameManager.Instance.IsGamePlaying() &&
           !KitchenGameManager.Instance.IsWaitingToStart()){
            return;
        }

        if(GetInput(out PlayerNetworkInput input)){
            HandleMovement(input);
            HandleCounterSelection(input);

            // Only host executes interactions (spawn/modify networked objects)
            if(Object.HasStateAuthority){
                NetworkButtons pressed = input.buttons.GetPressed(PreviousButtons);
                if(pressed.IsSet(InputButton.Interact)){
                    if(selectedCounter != null){
                        selectedCounter.Interact(this);
                    }
                }
                if(pressed.IsSet(InputButton.InteractAlternate)){
                    if(selectedCounter != null){
                        selectedCounter.InteractAlternate(this);
                    }
                }
                PreviousButtons = input.buttons;
            }
        }
    }

    private void HandleCounterSelection(PlayerNetworkInput input){
        Vector2 inputVector = input.moveDirection;
        Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

        if(moveDir != Vector3.zero){
            lastInteractDir = moveDir;
        }
        float interactDistance = 2f;
        if(Physics.Raycast(transform.position, lastInteractDir, out RaycastHit raycastHit, interactDistance, countersLayerMask)){
            if(raycastHit.transform.TryGetComponent(out BaseCounter baseCounter)){
                if(baseCounter != selectedCounter){
                    SetSelectedCounter(baseCounter);
                }
            }else{
                SetSelectedCounter(null);
            }
        }else{
            SetSelectedCounter(null);
        }
    }

    private void HandleMovement(PlayerNetworkInput input){
        Vector2 inputVector = input.moveDirection;
        Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

        float moveDistance = moveSpeed * Runner.DeltaTime;

        if(moveDir != Vector3.zero){
            controller.Move(moveDir * moveDistance);
        }

        IsWalking = moveDir != Vector3.zero;
        if(IsWalking){
            float rotateSpeed = 10f;
            transform.forward = Vector3.Slerp(transform.forward, moveDir, Runner.DeltaTime * rotateSpeed);
        }
    }

    private void SetSelectedCounter(BaseCounter selectedCounter){
        this.selectedCounter = selectedCounter;
        OnSelectedCounterChanged?.Invoke(this, new OnSelectedCounterChangedEventArgs {
            selectedCounter = selectedCounter
        });
    }

    public Transform GetKitchenObjectFollowTransform(){
        return kitchenObjectHoldPoint;
    }
    public void SetKitchenObject(KitchenObject kitchenObject){
        this.kitchenObject = kitchenObject;

        if(kitchenObject != null){
            OnPickedSomething?.Invoke(this, EventArgs.Empty);
            OnAnyPickedSomething?.Invoke(this, EventArgs.Empty);
        }
    }
    public KitchenObject GetKitchenObject(){
        return this.kitchenObject;
    }
    public void ClearKitchenObeject(){
        this.kitchenObject = null;
    }
    public bool HasKitchenObject(){
        return kitchenObject != null;
    }
    public NetworkObject GetNetworkObject(){
        return Object;
    }
}
