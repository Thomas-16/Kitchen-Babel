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
    Vector3             _renderErrorPos;
    Quaternion          _renderErrorRot;

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
      _renderErrorPos = Vector3.zero;
      _renderErrorRot = Quaternion.identity;
    }


    public void Jump(bool ignoreGrounded = false, float? overrideImpulse = null) {
      if (Data.Grounded || ignoreGrounded) {
        var newVel = Data.Velocity;
        newVel.y      += overrideImpulse ?? jumpImpulse;
        Data.Velocity =  newVel;
      }
    }

    public void Move(Vector3 direction) {
      var deltaTime    = Runner.DeltaTime;
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
    }
    
    public override void Spawned() {
      _initial = default;
      TryGetComponent(out _controller);
      // Without disabling and re-enabling the CharacterController here, the first Move call will reset the position to 0,0,0 instead of
      // keeping the position it was spawned at. Presumably disabling it clears some kind of internally cached "previous position" value
      _controller.enabled = false;
      _controller.enabled = true;
      CopyToBuffer();
    }

    public override void Render() {
      // Error-offset smoothing for the local client player to absorb prediction corrections
      if (Runner != null && !Runner.IsServer && Object != null && Object.HasInputAuthority) {
        // 1. Capture pre-correction position and rotation
        Vector3 prePos = transform.position;
        Quaternion preRot = transform.rotation;

        // 2. Apply the authoritative render state (may snap the transform on correction)
        NetworkTRSP.Render(this, transform, false, false, false, ref _initial);

        // 3. Accumulate the correction delta into the error offset
        _renderErrorPos += prePos - transform.position;
        _renderErrorRot = (preRot * Quaternion.Inverse(transform.rotation)) * _renderErrorRot;

        // 4. If error is too large (e.g. teleport), snap immediately
        if (_renderErrorPos.sqrMagnitude > 4f) {
          _renderErrorPos = Vector3.zero;
          _renderErrorRot = Quaternion.identity;
        }

        // 5. Decay error toward zero (~12 gives ~150ms absorption at 60fps)
        float t = 1f - Mathf.Exp(-12f * Time.deltaTime);
        _renderErrorPos = Vector3.Lerp(_renderErrorPos, Vector3.zero, t);
        _renderErrorRot = Quaternion.Slerp(_renderErrorRot, Quaternion.identity, t);

        // 6. Snap negligible error to zero to prevent float drift
        if (_renderErrorPos.sqrMagnitude < 1e-6f)
          _renderErrorPos = Vector3.zero;
        if (Quaternion.Angle(_renderErrorRot, Quaternion.identity) < 0.01f)
          _renderErrorRot = Quaternion.identity;

        // 7. Apply error offset to the rendered position
        _controller.enabled = false;
        transform.SetPositionAndRotation(
          transform.position + _renderErrorPos,
          _renderErrorRot * transform.rotation
        );
        _controller.enabled = true;
      } else {
        // Host or non-input-authority: render normally
        NetworkTRSP.Render(this, transform, false, false, false, ref _initial);
      }
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