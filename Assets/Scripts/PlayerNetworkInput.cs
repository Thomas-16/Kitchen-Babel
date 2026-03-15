using Fusion;
using UnityEngine;

public enum InputButton
{
    Interact,
    InteractAlternate,
}

public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 moveDirection;
    public NetworkButtons buttons;
}
