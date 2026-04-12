using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    [SerializeField] private float speed = 3f;

    private Vector3 pushDirection;

    public Vector3 Velocity => pushDirection * speed;

    private void Awake()
    {
        pushDirection = transform.right.normalized;
        Debug.Log($"[ConveyorBelt] Awake on '{gameObject.name}' | pushDirection={pushDirection} | speed={speed} | Velocity={Velocity}");
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent<Player>(out Player player))
        {
            Debug.Log($"[ConveyorBelt] OnTriggerStay Player | Velocity={Velocity}");
            player.AccumulateConveyorVelocity(Velocity);
        }
    }
}
