using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    private static readonly int SpeedShaderId = Shader.PropertyToID("_Speed");

    [SerializeField] private float speed = 3f;
    [SerializeField] private float animationSpeedMultiplier = 1f;
    [SerializeField] private Renderer beltRenderer;

    private Vector3 pushDirection;
    private MaterialPropertyBlock materialPropertyBlock;

    public Vector3 Velocity => pushDirection * speed;

    private void Awake()
    {
        pushDirection = -transform.right.normalized;
        InitializeRenderer();
        ApplyAnimationSpeed();
    }

    private void OnValidate()
    {
        pushDirection = -transform.right.normalized;
        InitializeRenderer();
        ApplyAnimationSpeed();
    }

    private void InitializeRenderer()
    {
        if (beltRenderer == null)
        {
            beltRenderer = transform.parent != null
                ? transform.parent.GetComponentInChildren<Renderer>()
                : GetComponentInChildren<Renderer>();
        }

        materialPropertyBlock ??= new MaterialPropertyBlock();
    }

    private void ApplyAnimationSpeed()
    {
        if (beltRenderer == null)
        {
            return;
        }

        beltRenderer.GetPropertyBlock(materialPropertyBlock);
        materialPropertyBlock.SetFloat(SpeedShaderId, Mathf.Abs(speed) * animationSpeedMultiplier);
        beltRenderer.SetPropertyBlock(materialPropertyBlock);
    }
}
