using TMPro;
using UnityEngine;

namespace FF.UI.Tooltips
{
    public class TooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Vector2 padding = new Vector2(12f, 8f);

        public void SetText(string text)
        {
            if (label == null || background == null)
            {
                return;
            }

            label.text = text;
            Canvas.ForceUpdateCanvases();

            Vector2 preferred = label.GetPreferredValues(text, 0f, 0f);
            label.rectTransform.sizeDelta = preferred;
            background.sizeDelta = preferred + padding * 2f;
        }

        public void SetPosition(Vector2 screenPosition, Vector2 offset, Canvas canvas)
        {
            if (background == null || canvas == null)
            {
                return;
            }

            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition + offset,
                camera,
                out Vector2 localPoint);

            Vector2 clampedPosition = ClampToCanvas(localPoint, canvasRect);
            background.anchoredPosition = clampedPosition;
        }

        private Vector2 ClampToCanvas(Vector2 position, RectTransform canvasRect)
        {
            Vector2 halfSize = background.sizeDelta * 0.5f;
            Vector2 min = canvasRect.rect.min + halfSize;
            Vector2 max = canvasRect.rect.max - halfSize;

            return new Vector2(
                Mathf.Clamp(position.x, min.x, max.x),
                Mathf.Clamp(position.y, min.y, max.y));
        }
    }
}
