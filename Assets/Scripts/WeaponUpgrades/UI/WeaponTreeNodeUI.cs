using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WeaponUpgrades.UI
{
    public class WeaponTreeNodeUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image stateFrame;
        [SerializeField] private Color lockedColor = Color.gray;
        [SerializeField] private Color availableColor = Color.yellow;
        [SerializeField] private Color unlockedColor = Color.green;

        public RectTransform RectTransform => (RectTransform)transform;

        public void SetIcon(Sprite icon)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
        }

        public void SetState(NodeState state)
        {
            if (stateFrame == null)
            {
                return;
            }

            switch (state)
            {
                case NodeState.Locked:
                    stateFrame.color = lockedColor;
                    break;
                case NodeState.Available:
                    stateFrame.color = availableColor;
                    break;
                case NodeState.Unlocked:
                    stateFrame.color = unlockedColor;
                    break;
            }
        }

        public void SetOnClick(UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }

    public enum NodeState
    {
        Locked,
        Available,
        Unlocked
    }
}
