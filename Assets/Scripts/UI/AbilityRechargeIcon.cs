using System;
using System.Collections.Generic;
using TMPro;
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
        [SerializeField] private TMP_Text dashCounterText;

        [Header("Defaults")]
        [SerializeField] private Sprite defaultIcon;
        [SerializeField] private Sprite defaultFillSprite;
        [SerializeField] private Color defaultFillColor = Color.white;
        [SerializeField] private List<AbilityIcon> abilityIcons = new();

        [Header("Full Animation")]
        [SerializeField, Min(0f)] private float fullScaleMultiplier = 1.12f;
        [SerializeField, Min(0f)] private float fullScaleDuration = 1f;

        private readonly Dictionary<CharacterAbilityController.AbilityType, AbilityIcon> _iconLookup = new();
        private CharacterAbilityController.AbilityType _currentAbility;
        private Coroutine _fullScaleRoutine;
        private Vector3 _baseScale;
        private bool _wasFull;
        private bool _isSubscribed;

        private void Awake()
        {
            if (!abilityController)
            {
                abilityController = FindAnyObjectByType<CharacterAbilityController>();
            }

            if (!canvasGroup)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            BuildLookup();
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            SetAbilityController(abilityController);
        }

        private void OnDisable()
        {
            SetAbilityController(null);

            if (_fullScaleRoutine != null)
            {
                StopCoroutine(_fullScaleRoutine);
                _fullScaleRoutine = null;
            }

            // Ensure transform scale is restored when disabled
            transform.localScale = _baseScale;
            _wasFull = false;
        }

        private void Update()
        {
            if (!abilityController)
            {
                SetAbilityController(FindAnyObjectByType<CharacterAbilityController>());
            }
        }

        private void SetAbilityController(CharacterAbilityController controller)
        {
            // Only early-out if the controller is the same AND it is a valid (non-null) object
            // This avoids the case where both abilityController and controller are null but
            // _isSubscribed is still true (destroyed controller) which would skip the cleanup.
            if (abilityController == controller && _isSubscribed && controller != null)
            {
                return;
            }

            if (_isSubscribed && abilityController)
            {
                abilityController.OnAbilityRechargeUpdated -= HandleAbilityRechargeUpdated;
                abilityController.OnDashChargesChanged -= HandleDashChargesChanged;
            }

            abilityController = controller;
            _isSubscribed = false;

            if (abilityController)
            {
                abilityController.OnAbilityRechargeUpdated += HandleAbilityRechargeUpdated;
                abilityController.OnDashChargesChanged += HandleDashChargesChanged;
                _isSubscribed = true;
                HandleAbilityRechargeUpdated(
                    abilityController.ActiveAbility,
                    abilityController.AbilityRechargeProgress,
                    abilityController.AbilityUsesRecharge);
            }
            else
            {
                // No controller -> hide UI and reset state
                SetVisible(false);
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

        private void HandleDashChargesChanged(int charges)
        {
            if (dashCounterText != null)
            {
                dashCounterText.text = charges.ToString();
            }
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

            float clamped = Mathf.Clamp01(progress);
            fillImage.fillAmount = clamped;

            // trigger full animation when the fill reaches 1.0 (and wasn't full previously)
            if (clamped >= 1f)
            {
                if (!_wasFull)
                {
                    _wasFull = true;
                    TriggerFullScale();
                }
            }
            else
            {
                _wasFull = false;
            }
        }

        private void TriggerFullScale()
        {
            if (_fullScaleRoutine != null)
            {
                StopCoroutine(_fullScaleRoutine);
            }

            _fullScaleRoutine = StartCoroutine(FullScaleRoutine());
        }

        private System.Collections.IEnumerator FullScaleRoutine()
        {
            float duration = Mathf.Max(0.0001f, fullScaleDuration);
            float half = duration * 0.5f;
            Vector3 start = transform.localScale;
            Vector3 target = _baseScale * Mathf.Max(0f, fullScaleMultiplier);

            // scale up (first half)
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / half);
                float smooth = Mathf.SmoothStep(0f, 1f, p);
                transform.localScale = Vector3.Lerp(start, target, smooth);
                yield return null;
            }

            // ensure max
            transform.localScale = target;

            // scale back down (second half)
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / half);
                float smooth = Mathf.SmoothStep(0f, 1f, p);
                transform.localScale = Vector3.Lerp(target, _baseScale, smooth);
                yield return null;
            }

            transform.localScale = _baseScale;
            _fullScaleRoutine = null;
        }
    }
}
