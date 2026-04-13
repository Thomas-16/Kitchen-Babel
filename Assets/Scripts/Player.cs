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

    [SerializeField] private LayerMask countersLayerMask;
    [SerializeField] private Transform kitchenObjectHoldPoint;

    [SerializeField] private NetworkCharacterController networkController;

    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;

    [Networked] public NetworkBool IsWalking { get; set; }
    [Networked] public int ColorIndex { get; set; }
    [Networked] public NetworkString<_32> PlayerName { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] private TickTimer DashTimer { get; set; }
    [Networked] private TickTimer DashCooldownTimer { get; set; }
    [Networked] private Vector3 DashDirection { get; set; }

    private ChangeDetector changeDetector;

    private Vector3 lastInteractDir;
    private BaseCounter selectedCounter;
    private KitchenObject kitchenObject;
    private CharacterController characterController;
    private readonly Collider[] conveyorOverlapResults = new Collider[8];

    private void Awake(){
        characterController = GetComponent<CharacterController>();
    }

    public static void ResetStaticData(){
        OnAnyPlayerSpawned = null;
        OnAnyPickedSomething = null;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (LocalInstance == this)
            LocalInstance = null;
    }

    public override void Spawned(){
        Debug.Log($"[Player] Spawned! HasInputAuthority={HasInputAuthority}, HasStateAuthority={HasStateAuthority}, InputAuthority={Object.InputAuthority}");
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if(HasInputAuthority){
            LocalInstance = this;

            OnAnyPlayerSpawned?.Invoke(this, EventArgs.Empty);
        }
    }

    public override void Render(){
        if(changeDetector == null) return;
        foreach(var change in changeDetector.DetectChanges(this)){
            if(change == nameof(DashCooldownTimer)){
                if(SoundManager.Instance != null){
                    SoundManager.Instance.PlayDashSound(transform.position);
                }
            }
        }
    }

    public override void FixedUpdateNetwork(){
        if(KitchenGameManager.Instance == null) return;

        if(!KitchenGameManager.Instance.IsGamePlaying() &&
           !KitchenGameManager.Instance.IsWaitingToStart()){
            return;
        }

        if(GetInput(out PlayerNetworkInput input)){
            // Host: override remote player position with client's reported position
            if(HasStateAuthority && !HasInputAuthority && input.position.sqrMagnitude > 0f){
                networkController.SetPosition(input.position, input.rotation);
            }

            NetworkButtons pressed = input.buttons.GetPressed(PreviousButtons);
            PreviousButtons = input.buttons;

            if(pressed.IsSet(InputButton.Dash) && DashCooldownTimer.ExpiredOrNotRunning(Runner)){
                Vector3 dir = new Vector3(input.moveDirection.x, 0f, input.moveDirection.y);
                if(dir.sqrMagnitude < 0.01f) dir = transform.forward;
                DashDirection = dir.normalized;
                DashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
                DashCooldownTimer = TickTimer.CreateFromSeconds(Runner, dashCooldown);
            }

            HandleMovement(input);

            // Only run raycasts on forward ticks, not during resimulation.
            // Physics state isn't rewound during resim, so raycasts return
            // different results → prediction diverges → jitter.
            if(Runner.IsForward){
                HandleCounterSelection(input);
            }

            // Only host executes interactions (spawn/modify networked objects)
            if(Object.HasStateAuthority){
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

        Vector3 conveyorVel = GetConveyorVelocity();
        Vector3 dashVel = DashTimer.ExpiredOrNotRunning(Runner) ? Vector3.zero : DashDirection * dashSpeed;
        networkController.Move(moveDir, conveyorVel + dashVel);

        IsWalking = moveDir != Vector3.zero;
    }

    private Vector3 GetConveyorVelocity(){
        Vector3 conveyorVelocity = Vector3.zero;
        if(characterController == null){
            return conveyorVelocity;
        }

        Bounds bounds = characterController.bounds;
        float radius = Mathf.Max(0.01f, characterController.radius * 0.9f);
        Vector3 capsuleOffset = Vector3.up * radius;
        Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + capsuleOffset;
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) - capsuleOffset;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            conveyorOverlapResults,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for(int i = 0; i < hitCount; i++){
            Collider overlapCollider = conveyorOverlapResults[i];
            if(overlapCollider == null){
                continue;
            }

            if(overlapCollider.TryGetComponent(out ConveyorBelt conveyor)){
                conveyorVelocity += conveyor.Velocity;
            }
        }

        return conveyorVelocity;
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
