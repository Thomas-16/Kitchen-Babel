namespace Fusion {
  using System.Runtime.CompilerServices;
  using System.Runtime.InteropServices;
  using UnityEngine;

  [StructLayout(LayoutKind.Explicit)]
  [NetworkStructWeaved(WORDS + 4)]
  public unsafe struct NetworkCCData : INetworkStruct {
    public const int WORDS = NetworkTRSPData.WORDS + 4;
    public const int SIZE  = WORDS * 4;

    [FieldOffset(0)]
    public NetworkTRSPData TRSPData;

    [FieldOffset((NetworkTRSPData.WORDS + 0) * Allocator.REPLICATE_WORD_SIZE)]
    int _grounded;

    [FieldOffset((NetworkTRSPData.WORDS + 1) * Allocator.REPLICATE_WORD_SIZE)]
    Vector3Compressed _velocityData;

    public bool Grounded {
      get => _grounded == 1;
      set => _grounded = (value ? 1 : 0);
    }

    public Vector3 Velocity {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _velocityData;
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      set => _velocityData = value;
    }
  }

  [DisallowMultipleComponent]
  [RequireComponent(typeof(CharacterController))]
  [NetworkBehaviourWeaved(NetworkCCData.WORDS)]
  // ReSharper disable once CheckNamespace
  public sealed unsafe class NetworkCharacterController : NetworkTRSP, INetworkTRSPTeleport, IBeforeAllTicks, IAfterAllTicks, IBeforeCopyPreviousState {
    new ref NetworkCCData Data => ref ReinterpretState<NetworkCCData>();

    [Header("Character Controller Settings")]
    public float gravity = -20.0f;
    public float jumpImpulse   = 8.0f;
    public float acceleration  = 10.0f;
    public float braking       = 10.0f;
    public float maxSpeed      = 2.0f;
    public float rotationSpeed = 15.0f;

    Tick                _initial;
    CharacterController _controller;
    Vector3             _clientPos;
    Quaternion          _clientRot;
    bool                _clientInitialized;

    public Vector3 Velocity {
      get => Data.Velocity;
      set => Data.Velocity = value;
    }

    public bool Grounded {
      get => Data.Grounded;
      set => Data.Grounded = value;
    }

    public void Teleport(Vector3? position = null, Quaternion? rotation = null) {
      _controller.enabled = false;
      NetworkTRSP.Teleport(this, transform, position, rotation);
      _controller.enabled = true;
    }


    public void Jump(bool ignoreGrounded = false, float? overrideImpulse = null) {
      if (Data.Grounded || ignoreGrounded) {
        var newVel = Data.Velocity;
        newVel.y      += overrideImpulse ?? jumpImpulse;
        Data.Velocity =  newVel;
      }
    }

    public void SetPosition(Vector3 position, Quaternion rotation) {
      _controller.enabled = false;
      transform.SetPositionAndRotation(position, rotation);
      _controller.enabled = true;
    }

    public void Move(Vector3 direction) {
      var deltaTime = Runner.DeltaTime;

      // Client-authoritative: on forward ticks, simulate from client's own position
      bool isLocalForward = !Runner.IsServer && Object.HasInputAuthority && Runner.IsForward;
      if (isLocalForward && _clientInitialized) {
        _controller.enabled = false;
        transform.SetPositionAndRotation(_clientPos, _clientRot);
        _controller.enabled = true;
      }

      var previousPos  = transform.position;
      var moveVelocity = Data.Velocity;

      direction = direction.normalized;

      if (Data.Grounded && moveVelocity.y < 0) {
        moveVelocity.y = 0f;
      }

      moveVelocity.y += gravity * Runner.DeltaTime;

      var horizontalVel = default(Vector3);
      horizontalVel.x = moveVelocity.x;
      horizontalVel.z = moveVelocity.z;

      if (direction == default) {
        horizontalVel = Vector3.Lerp(horizontalVel, default, braking * deltaTime);
      } else {
        horizontalVel      = Vector3.ClampMagnitude(horizontalVel + direction * acceleration * deltaTime, maxSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Runner.DeltaTime);
      }

      moveVelocity.x = horizontalVel.x;
      moveVelocity.z = horizontalVel.z;

      _controller.Move(moveVelocity * deltaTime);

      Data.Velocity = (transform.position - previousPos) * Runner.TickRate;
      Data.Grounded = _controller.isGrounded;

      // Save client's simulation result from forward ticks
      if (isLocalForward) {
        _clientPos = transform.position;
        _clientRot = transform.rotation;
        _clientInitialized = true;
      }
    }
    
    public override void Spawned() {
      _initial = default;
      _clientInitialized = false;
      TryGetComponent(out _controller);
      // Without disabling and re-enabling the CharacterController here, the first Move call will reset the position to 0,0,0 instead of
      // keeping the position it was spawned at. Presumably disabling it clears some kind of internally cached "previous position" value
      _controller.enabled = false;
      _controller.enabled = true;
      CopyToBuffer();
    }

    public override void Render() {
      // Client-authoritative: show the client's own accumulated position
      if (!Runner.IsServer && Object.HasInputAuthority && _clientInitialized) {
        _controller.enabled = false;
        transform.SetPositionAndRotation(_clientPos, _clientRot);
        _controller.enabled = true;
        return;
      }

      NetworkTRSP.Render(this, transform, false, false, false, ref _initial);
    }

    void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount) {
      CopyToEngine();
    }

    void IAfterAllTicks.AfterAllTicks(bool resimulation, int tickCount) {
      CopyToBuffer();
    }

    void IBeforeCopyPreviousState.BeforeCopyPreviousState() {
      CopyToBuffer();
    }
    
    void Awake() {
      TryGetComponent(out _controller);
    }

    void CopyToBuffer() {
      Data.TRSPData.Position = transform.position;
      Data.TRSPData.Rotation = transform.rotation;
    }

    void CopyToEngine() {
      // CC must be disabled before resetting the transform state
      _controller.enabled = false;

      // set position and rotation
      transform.SetPositionAndRotation(Data.TRSPData.Position, Data.TRSPData.Rotation);

      // Re-enable CC
      _controller.enabled = true;
    }
  }
}