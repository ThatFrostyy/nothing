using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("Camera Follow Settings")]
        [SerializeField] Transform target;
        [SerializeField] Camera cam;
        [SerializeField, Range(0f, 20f)] float followSpeed = 10f;
        [SerializeField, Range(0f, 1f)] float mouseInfluence = 0.2f;

        Vector3 velocity;

        void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(CameraFollow2D)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
                return;
            }
        }

        void OnValidate()
        {
            if (!cam)
            {
                cam = GetComponent<Camera>();
            }

            if (!target && transform.parent)
            {
                target = transform.parent;
            }
        }

        bool ValidateDependencies()
        {
            bool ok = true;

            if (!cam)
            {
                Debug.LogError("Missing Camera reference.", this);
                ok = false;
            }

            if (!target)
            {
                Debug.LogError("Missing follow target reference.", this);
                ok = false;
            }

            return ok;
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

            Vector2 padding = Vector2.zero;
            if (cam && cam.orthographic)
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;
                padding = new Vector2(halfWidth, halfHeight);
            }

            if (Ground.Instance)
            {
                smoothed = Ground.Instance.ClampPoint(smoothed, padding);
            }

            smoothed.z = -10f;
            transform.position = smoothed;
        }
    }
}
