using TMPro;
using UnityEngine;

namespace FF
{
    public class CharacterLevelTooltip : MonoBehaviour
    {
        [SerializeField] private TMP_Text tooltipLabel;
        [SerializeField] private Vector2 padding = new(12f, 8f);

        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        public void Show(string text, Vector2 screenPosition, Canvas parentCanvas)
        {
            if (tooltipLabel == null || _rectTransform == null)
            {
                return;
            }

            tooltipLabel.text = text ?? string.Empty;

            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }

            if (parentCanvas == null)
            {
                return;
            }

            RectTransform canvasRect = parentCanvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Camera worldCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : parentCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    worldCamera,
                    out Vector2 localPoint))
            {
                _rectTransform.anchoredPosition = localPoint + padding;
            }
        }
    }
}
