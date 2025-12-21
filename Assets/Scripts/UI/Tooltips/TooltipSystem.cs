using UnityEngine;

namespace FF.UI.Tooltips
{
    public class TooltipSystem : MonoBehaviour
    {
        public static TooltipSystem Instance { get; private set; }

        public static TooltipSystem GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            TooltipSystem existing = FindObjectOfType<TooltipSystem>();
            if (existing != null)
            {
                return existing;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            GameObject systemObject = new GameObject("TooltipSystem");
            systemObject.transform.SetParent(canvas.transform, false);
            return systemObject.AddComponent<TooltipSystem>();
        }

        [SerializeField] private TooltipView tooltipPrefab;
        [SerializeField] private Canvas targetCanvas;

        private TooltipView activeTooltip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (targetCanvas == null)
            {
                targetCanvas = GetComponentInParent<Canvas>();
            }

            if (targetCanvas == null)
            {
                targetCanvas = FindObjectOfType<Canvas>();
            }
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            EnsureTooltip();
            if (activeTooltip == null)
            {
                return;
            }

            activeTooltip.SetText(text);
            activeTooltip.gameObject.SetActive(true);
            UpdatePosition(Input.mousePosition);
        }

        public void UpdatePosition(Vector2 screenPosition)
        {
            if (activeTooltip == null || targetCanvas == null)
            {
                return;
            }

            activeTooltip.SetPosition(screenPosition, targetCanvas);
        }

        public void Hide()
        {
            if (activeTooltip != null)
            {
                activeTooltip.gameObject.SetActive(false);
            }
        }

        private void EnsureTooltip()
        {
            if (activeTooltip != null)
            {
                return;
            }

            if (tooltipPrefab == null)
            {
                tooltipPrefab = Resources.Load<TooltipView>("Prefabs/Tooltip");
            }

            if (tooltipPrefab == null || targetCanvas == null)
            {
                return;
            }

            activeTooltip = Instantiate(tooltipPrefab, targetCanvas.transform);
            activeTooltip.gameObject.SetActive(false);
        }
    }
}
