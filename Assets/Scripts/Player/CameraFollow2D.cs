using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Camera cam;
        [SerializeField, Range(0f, 20f)] float followSpeed = 10f;
        [SerializeField, Range(0f, 1f)] float mouseInfluence = 0.2f;

        Vector3 velocity;

        void Awake()
        {
            if (!cam) cam = Camera.main;
        }

        void LateUpdate()
        {
            if (!target) return;

            Vector3 basePos = target.position;

            // Smooth camera drift toward mouse
            Vector3 mouseWorld = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector3 dirToMouse = (mouseWorld - basePos);
            dirToMouse.z = 0;

            Vector3 desiredPos = basePos + dirToMouse * mouseInfluence;

            Vector3 smoothed = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, 1f / followSpeed);

            smoothed.z = -10f; 
            transform.position = smoothed;
        }
    }
}
