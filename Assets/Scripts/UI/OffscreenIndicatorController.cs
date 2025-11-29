using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public class OffscreenIndicatorController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RectTransform indicatorContainer;
        [SerializeField] private RectTransform pickupIndicatorPrefab;
        [SerializeField] private RectTransform weaponCrateIndicatorPrefab;
        [SerializeField] private RectTransform bossIndicatorPrefab;
        [SerializeField, Min(0f)] private float screenEdgeBuffer = 32f;
        [SerializeField, Min(0f)] private float minimumDistanceFromCenter = 48f;

        private readonly Dictionary<Transform, RectTransform> activeIndicators = new();
        private readonly HashSet<Transform> seenThisFrame = new();
        private Canvas parentCanvas;

        void Awake()
        {
            ResolveCamera();

            parentCanvas = indicatorContainer ? indicatorContainer.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();

            if (!indicatorContainer && parentCanvas)
            {
                indicatorContainer = parentCanvas.GetComponent<RectTransform>();
            }
        }

        void OnEnable()
        {
            ResolveCamera();
        }

        void LateUpdate()
        {
            if (!targetCamera)
            {
                ResolveCamera();
            }

            if (!targetCamera || indicatorContainer == null)
            {
                return;
            }

            seenThisFrame.Clear();
            RefreshIndicators(UpgradePickup.ActivePickups, pickupIndicatorPrefab);
            RefreshIndicators(WeaponCrate.ActiveCrates, weaponCrateIndicatorPrefab);
            RefreshIndicators(Enemy.ActiveBosses, bossIndicatorPrefab);
            CleanupUnusedIndicators();
        }

        void OnEnable()
        {
            foreach (var kvp in activeIndicators)
            {
                if (kvp.Value)
                    Destroy(kvp.Value.gameObject);
            }

            activeIndicators.Clear();
            seenThisFrame.Clear();
        }

        void OnDisable()
        {
            foreach (var kvp in activeIndicators)
            {
                if (kvp.Value)
                    Destroy(kvp.Value.gameObject);
            }

            activeIndicators.Clear();
            seenThisFrame.Clear();
        }

        private void RefreshIndicators(IEnumerable<UpgradePickup> pickups, RectTransform prefab)
        {
            if (prefab == null || pickups == null)
            {
                return;
            }

            foreach (var pickup in pickups)
            {
                if (!pickup)
                {
                    continue;
                }

                EnsureIndicator(pickup.transform, prefab);
            }
        }

        private void RefreshIndicators(IEnumerable<WeaponCrate> crates, RectTransform prefab)
        {
            if (prefab == null || crates == null)
            {
                return;
            }

            foreach (var crate in crates)
            {
                if (!crate)
                {
                    continue;
                }

                EnsureIndicator(crate.transform, prefab);
            }
        }

        private void RefreshIndicators(IEnumerable<Enemy> enemies, RectTransform prefab)
        {
            if (prefab == null || enemies == null)
            {
                return;
            }

            foreach (var enemy in enemies)
            {
                if (!enemy)
                {
                    continue;
                }

                EnsureIndicator(enemy.transform, prefab);
            }
        }

        private void EnsureIndicator(Transform target, RectTransform prefab)
        {
            seenThisFrame.Add(target);
            if (!activeIndicators.TryGetValue(target, out var indicator))
            {
                indicator = Instantiate(prefab, indicatorContainer);
                indicator.gameObject.SetActive(true);
                activeIndicators[target] = indicator;
            }

            UpdateIndicator(indicator, target);
        }

        private void CleanupUnusedIndicators()
        {
            if (activeIndicators.Count == 0)
            {
                return;
            }

            var toRemove = new List<Transform>();
            foreach (var kvp in activeIndicators)
            {
                if (!seenThisFrame.Contains(kvp.Key) || !kvp.Key)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                if (activeIndicators.TryGetValue(toRemove[i], out var rect) && rect)
                {
                    Destroy(rect.gameObject);
                }

                activeIndicators.Remove(toRemove[i]);
            }
        }

        private void UpdateIndicator(RectTransform indicator, Transform target)
        {
            if (!indicator || !target)
            {
                return;
            }

            Vector3 viewport = targetCamera.WorldToViewportPoint(target.position);
            bool isVisible = viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
            if (isVisible)
            {
                indicator.gameObject.SetActive(false);
                return;
            }

            indicator.gameObject.SetActive(true);

            if (viewport.z < 0f)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
                viewport.z = 0f;
            }

            float clampedX = Mathf.Clamp(viewport.x, 0f, 1f);
            float clampedY = Mathf.Clamp(viewport.y, 0f, 1f);

            float screenX = Mathf.Lerp(screenEdgeBuffer, Screen.width - screenEdgeBuffer, clampedX);
            float screenY = Mathf.Lerp(screenEdgeBuffer, Screen.height - screenEdgeBuffer, clampedY);

            Vector2 screenPos = new(screenX, screenY);
            Vector2 screenCenter = new(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 direction = (screenPos - screenCenter);
            float distance = Mathf.Max(minimumDistanceFromCenter, direction.magnitude);
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            screenPos = screenCenter + direction * distance;

            Camera canvasCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? parentCanvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(indicatorContainer, screenPos, canvasCamera, out Vector2 anchored))
            {
                indicator.anchoredPosition = anchored;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            indicator.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }
    }
}
