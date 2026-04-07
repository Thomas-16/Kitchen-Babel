using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    private enum Mode{
        Lookat,
        LookAtInverted,
        CameraForward,
        CameraForwardInverted,
    }

    [SerializeField] private Mode mode;

    [Header("Axis Locks")]
    [SerializeField] private bool lockX;
    [SerializeField] private bool lockY;
    [SerializeField] private bool lockZ;

    private void LateUpdate() {
        Quaternion originalRotation = transform.rotation;

        switch(mode){
            case Mode.Lookat:
                transform.LookAt(Camera.main.transform);
                break;
            case Mode.LookAtInverted:
                Vector3 dirFromCamera = transform.position - Camera.main.transform.position;
                transform.LookAt(transform.position + dirFromCamera);
                break;
            case Mode.CameraForward:
                transform.forward = Camera.main.transform.forward;
                break;
            case Mode.CameraForwardInverted:
                transform.forward = -Camera.main.transform.forward;
                break;
        }

        if (lockX || lockY || lockZ) {
            Vector3 euler = transform.eulerAngles;
            Vector3 originalEuler = originalRotation.eulerAngles;
            if (lockX) euler.x = originalEuler.x;
            if (lockY) euler.y = originalEuler.y;
            if (lockZ) euler.z = originalEuler.z;
            transform.eulerAngles = euler;
        }
    }
}
