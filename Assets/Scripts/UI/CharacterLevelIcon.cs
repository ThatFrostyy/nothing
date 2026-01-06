using FF.UI.Tooltips;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class CharacterLevelIcon : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image highlightImage;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Color lockedColor = Color.white;
        [SerializeField] private Color unlockedColor = Color.green;
        [SerializeField] TooltipTrigger trigger;

        public void Configure(Sprite icon, int levelNumber, bool unlocked, string description)
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
            }

            if (levelText)
            {
                levelText.text = levelNumber > 0 ? levelNumber.ToString() : string.Empty;
                levelText.color = unlocked ? unlockedColor : lockedColor;
            }

            if (trigger)
            {
                // Enable the tooltip when there is useful text to show.
                bool hasDescription = !string.IsNullOrWhiteSpace(description);
                trigger.enabled = hasDescription;

                if (hasDescription)
                {
                    string state = unlocked ? "Unlocked" : "Locked";
                    trigger.SetText($"Level {levelNumber} - {state}\n{description}");
                }
            }
        }
    }
}
