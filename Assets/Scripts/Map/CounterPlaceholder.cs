using Fusion;
using UnityEngine;

/// <summary>
/// Place on an empty GameObject inside a map prefab to mark where a counter should spawn.
/// Replaces both FlexCounter and ShuffleableCounter as the unified marker system.
/// </summary>
public class CounterPlaceholder : MonoBehaviour
{
    [Header("Counter to Spawn (ignored if isFlex is true)")]
    [SerializeField] private NetworkPrefabRef counterPrefab;

    [Header("Randomization Flags")]
    [SerializeField] private bool isShuffleable;
    [SerializeField] private bool isFlex;

    [Header("Flex Options (only used when isFlex is true)")]
    // Do not place versus-only prefabs here — mode gating only applies to the non-flex counterPrefab path.
    [SerializeField] private NetworkPrefabRef[] flexOptions;

    [Header("Mode Gating")]
    [SerializeField] private bool versusOnly;

    public NetworkPrefabRef CounterPrefab => counterPrefab;
    public bool IsShuffleable => isShuffleable;
    public bool IsFlex => isFlex;
    public NetworkPrefabRef[] FlexOptions => flexOptions;
    public bool VersusOnly => versusOnly;

    private static readonly Vector3 CounterSize = new Vector3(1.5f, 1.5f, 1.5f);
    private static readonly Vector3 CounterCenter = new Vector3(0f, 0.5f, 0f);

    private void OnDrawGizmos()
    {
        if (versusOnly)
            Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.6f); // magenta
        else if (isFlex)
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.6f); // blue
        else if (isShuffleable)
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f); // yellow
        else
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.6f); // green

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(CounterCenter, CounterSize);
    }
}
