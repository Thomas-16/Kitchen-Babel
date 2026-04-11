using Fusion;
using UnityEngine;

/// <summary>
/// Place on an empty GameObject to mark a position where a random counter type should spawn.
/// Assign possible counter prefabs in the inspector.
/// </summary>
public class FlexCounter : MonoBehaviour
{
    [SerializeField] private NetworkPrefabRef[] possibleCounters;

    public NetworkPrefabRef[] PossibleCounters => possibleCounters;
}
