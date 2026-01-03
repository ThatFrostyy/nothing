using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class AbilityRechargeIcon : MonoBehaviour
    {
        [Serializable]
        private struct AbilityIcon
        {
            public CharacterAbilityController.AbilityType abilityType;
            public Sprite icon;       // background / static icon
            public Sprite fillSprite; // sprite used by the fill image (will be tinted & filled)
            public Color fillColor;
        }

        [Header("References")]
        [SerializeField] private CharacterAbilityController abilityController;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Defaults")]
        [SerializeField] private Sprite defaultIcon;
        [SerializeField] private Sprite defaultFillSprite;
        [SerializeField] private Color defaultFillColor = Color.white;
        [SerializeField] private List<AbilityIcon> abilityIcons = new();

        private readonly Dictionary<CharacterAbilityController.AbilityType, AbilityIcon> _iconLookup = new();
        private CharacterAbilityController.AbilityType _currentAbility;

        private void Awake()
        {
            if (!abilityController)
            {
                abilityController = FindObjectOfType<CharacterAbilityController>();
            }

            if (!canvasGroup)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            BuildLookup();
        }

        private void OnEnable()
        {
            if (abilityController)
            {
                abilityController.OnAbilityRechargeUpdated += HandleAbilityRechargeUpdated;
                HandleAbilityRechargeUpdated(
                    abilityController.ActiveAbility,
                    abilityController.AbilityRechargeProgress,
                    abilityController.AbilityUsesRecharge);
            }
        }

        private void OnDisable()
        {
            if (abilityController)
            {
                abilityController.OnAbilityRechargeUpdated -= HandleAbilityRechargeUpdated;
            }
        }

        private void BuildLookup()
        {
            _iconLookup.Clear();
            for (int i = 0; i < abilityIcons.Count; i++)
            {
                AbilityIcon entry = abilityIcons[i];
                _iconLookup[entry.abilityType] = entry;
            }
        }

        private void HandleAbilityRechargeUpdated(
            CharacterAbilityController.AbilityType ability,
            float progress,
            bool isVisible)
        {
            SetVisible(isVisible);
            UpdateIcon(ability);
            UpdateFill(progress);
        }

        private void SetVisible(bool isVisible)
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = isVisible ? 1f : 0f;
                canvasGroup.blocksRaycasts = isVisible;
                canvasGroup.interactable = isVisible;
                return;
            }

            gameObject.SetActive(isVisible);
        }

        private void UpdateIcon(CharacterAbilityController.AbilityType ability)
        {
            if (_currentAbility == ability)
            {
                return;
            }

            _currentAbility = ability;
            AbilityIcon iconData = default;
            bool hasIcon = _iconLookup.TryGetValue(ability, out iconData);

            Sprite iconSprite = hasIcon ? iconData.icon : defaultIcon;
            Sprite fillSprite = hasIcon ? iconData.fillSprite : defaultFillSprite;
            Color fillColor = hasIcon ? iconData.fillColor : defaultFillColor;

            if (iconImage)
            {
                // Icon is treated as the static background image (no fill behavior).
                iconImage.sprite = iconSprite;
            }

            if (fillImage)
            {
                // Fill image uses its own sprite and is tinted with the chosen color.
                fillImage.sprite = fillSprite;
                fillImage.color = fillColor;
            }
        }

        private void UpdateFill(float progress)
        {
            if (!fillImage)
            {
                return;
            }

            fillImage.fillAmount = Mathf.Clamp01(progress);
        }
    }
}
