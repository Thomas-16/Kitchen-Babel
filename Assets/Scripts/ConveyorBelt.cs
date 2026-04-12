using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    [SerializeField] private float speed = 3f;

    private Vector3 pushDirection;

    public Vector3 Velocity => pushDirection * speed;

    private void Awake()
    {
        pushDirection = -transform.right.normalized;
    }
}
