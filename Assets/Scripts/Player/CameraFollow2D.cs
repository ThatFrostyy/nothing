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
        [Header("Zoom")]
        [SerializeField] bool enableZoom = true;
        [SerializeField, Min(0.01f)] float zoomStep = 0.5f;
        [SerializeField, Min(0.01f)] float minZoom = 4f;
        [SerializeField, Min(0.01f)] float maxZoom = 12f;
        [SerializeField, Min(0f)] float zoomLerpSpeed = 6f;

        Vector3 velocity;
        float targetOrthoSize;

        void Awake()
        {
            if (!cam) cam = Camera.main;
            if (cam && cam.orthographic)
            {
                targetOrthoSize = cam.orthographicSize;
            }
        }

        void LateUpdate()
        {
            if (!target) return;

            UpdateZoom();

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

        void UpdateZoom()
        {
            if (!enableZoom || cam == null || !cam.orthographic)
            {
                return;
            }

            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (!Mathf.Approximately(scroll, 0f))
            {
                targetOrthoSize = Mathf.Clamp(
                    targetOrthoSize - scroll * zoomStep * 0.1f,
                    Mathf.Min(minZoom, maxZoom),
                    Mathf.Max(minZoom, maxZoom)
                );
            }

            float lerpSpeed = Mathf.Max(0f, zoomLerpSpeed);
            if (lerpSpeed <= 0f)
            {
                cam.orthographicSize = targetOrthoSize;
                return;
            }

            cam.orthographicSize = Mathf.Lerp(
                cam.orthographicSize,
                targetOrthoSize,
                Time.unscaledDeltaTime * lerpSpeed
            );
        }
    }
}
