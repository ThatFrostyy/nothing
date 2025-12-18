using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FF
{
    public class CharacterLevelIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image highlightImage;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Color lockedColor = Color.white;
        [SerializeField] private Color unlockedColor = Color.green;
        [SerializeField] private Color unlockedBackgroundColor = Color.white;

        private Action<string, Vector2> _onHover;
        private Action _onExit;
        private string _tooltipText = string.Empty;

        public void Configure(
            Sprite icon,
            int levelNumber,
            bool unlocked,
            string tooltip,
            Action<string, Vector2> onHover,
            Action onExit)
        {
            if (iconImage)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.color = unlocked ? unlockedColor : lockedColor;
            }

            if (highlightImage)
            {
                highlightImage.enabled = unlocked;
                highlightImage.color = unlocked ? unlockedBackgroundColor : highlightImage.color;
            }

            if (levelText)
            {
                levelText.text = levelNumber > 0 ? levelNumber.ToString() : string.Empty;
            }

            _tooltipText = tooltip ?? string.Empty;
            _onHover = onHover;
            _onExit = onExit;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(_tooltipText))
            {
                _onHover?.Invoke(_tooltipText, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onExit?.Invoke();
        }
    }
}
