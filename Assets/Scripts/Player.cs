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
        if(!Object.HasStateAuthority) return;

        if(KitchenGameManager.Instance.IsPaused) return;

        if(!KitchenGameManager.Instance.IsGamePlaying() &&
           !KitchenGameManager.Instance.IsWaitingToStart()){
            return;
        }

        if(GetInput(out PlayerNetworkInput input)){
            // Handle game start
            if(KitchenGameManager.Instance.IsWaitingToStart()){
                var pressed = input.buttons.GetPressed(PreviousButtons);
                if(pressed.IsSet(InputButton.Interact)){
                    KitchenGameManager.Instance.TryStartGame();
                }
                PreviousButtons = input.buttons;
                return;
            }

            HandleMovement(input);
            HandleInteractions(input);

            PreviousButtons = input.buttons;
        }
    }

    private void HandleInteractions(PlayerNetworkInput input){
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

        var pressed = input.buttons.GetPressed(PreviousButtons);

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
    }

    private void HandleMovement(PlayerNetworkInput input){
        Vector2 inputVector = input.moveDirection;
        Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

        float moveDistance = moveSpeed * Runner.DeltaTime;
        float playerRadius = .6f;
        float playerHeight = 2f;
        bool canMove = !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDir, moveDistance);

        if(!canMove){
            Vector3 moveDirX = new Vector3(moveDir.x, 0, 0).normalized;
            canMove = (moveDir.x < -.5f || moveDir.x > .5f) && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirX, moveDistance);
            if(canMove){
                moveDir = moveDirX;
            }else{
                Vector3 moveDirZ = new Vector3(0, 0, moveDir.z).normalized;
                canMove = (moveDir.z < -.5f || moveDir.z > .5f) && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirZ, moveDistance);

                if(canMove){
                    moveDir = moveDirZ;
                }
            }
        }
        if(canMove){
            transform.position += moveDir * moveDistance;
        }

        IsWalking = moveDir != Vector3.zero;
        float rotateSpeed = 10f;
        transform.forward = Vector3.Slerp(transform.forward, moveDir, Runner.DeltaTime * rotateSpeed);
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
