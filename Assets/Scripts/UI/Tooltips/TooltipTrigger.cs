using UnityEngine;
using UnityEngine.EventSystems;

namespace FF.UI.Tooltips
{
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField, TextArea] private string tooltipText;
        [SerializeField] private bool followCursor = true;
        [SerializeField] private TooltipSystem tooltipSystemOverride;

        public void OnPointerEnter(PointerEventData eventData)
        {
            TooltipSystem system = tooltipSystemOverride != null ? tooltipSystemOverride : TooltipSystem.GetOrCreate();
            if (system == null)
            {
                return;
            }

            system.Show(tooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipSystem system = tooltipSystemOverride != null ? tooltipSystemOverride : TooltipSystem.GetOrCreate();
            if (system == null)
            {
                return;
            }

            system.Hide();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!followCursor)
            {
                return;
            }

            TooltipSystem system = tooltipSystemOverride != null ? tooltipSystemOverride : TooltipSystem.GetOrCreate();
            if (system == null)
            {
                return;
            }

            system.UpdatePosition(eventData.position);
        }

        public void SetText(string text)
        {
            tooltipText = text;
        }

        private void OnDisable()
        {
            TooltipSystem system = tooltipSystemOverride != null ? tooltipSystemOverride : TooltipSystem.GetOrCreate();
            if (system == null)
            {
                return;
            }

            system.Hide();
        }
    }
}
