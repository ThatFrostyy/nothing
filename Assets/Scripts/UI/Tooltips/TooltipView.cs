using TMPro;
using UnityEngine;

namespace FF.UI.Tooltips
{
    public class TooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private TextMeshProUGUI label;

        public void SetText(string text)
        {
            if (label == null || background == null)
            {
                return;
            }

            // Let the prefab define sizes. Enable TMP auto-sizing so text scales to fit the prefab's text rect.
            label.enableAutoSizing = true;
            label.text = text;

            // Force updates so layout/mesh is refreshed immediately.
            label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
        }

        public void SetPosition(Vector2 screenPosition, Canvas canvas)
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
                screenPosition,
                camera,
                out Vector2 localPoint);

            // Ensure tooltip pivot is bottom-left so the provided screen point maps to the tooltip's bottom-left corner.
            if (background.pivot != Vector2.zero)
            {
                background.pivot = Vector2.zero;
            }

            Vector2 clampedPosition = ClampToCanvas(localPoint, canvasRect);
            background.anchoredPosition = clampedPosition;
        }

        private Vector2 ClampToCanvas(Vector2 position, RectTransform canvasRect)
        {
            // Use the background rect size and pivot to compute valid min/max within the canvas.
            Vector2 size = background.rect.size;
            Vector2 pivot = background.pivot;

            Vector2 min = canvasRect.rect.min + size * pivot;
            Vector2 max = canvasRect.rect.max - size * (Vector2.one - pivot);

            return new Vector2(
                Mathf.Clamp(position.x, min.x, max.x),
                Mathf.Clamp(position.y, min.y, max.y));
        }
    }
}
