using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class GrenadeChargeBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AutoShooter shooter;
        [SerializeField] private Image fillImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Appearance")]
        [SerializeField] private Color chargeColor = Color.red;
        [SerializeField, Min(0f)] private float hideDelay = 0.2f;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 8f;

        private float _currentCharge;
        private float _lastVisibleTime;

        private void Awake()
        {
            if (!shooter)
            {
                shooter = GetComponentInParent<AutoShooter>();
            }

            if (fillImage)
            {
                fillImage.fillAmount = 0f;
                fillImage.color = chargeColor;
            }
        }

        private void OnEnable()
        {
            if (shooter)
            {
                shooter.OnGrenadeChargeChanged += HandleChargeChanged;
                HandleChargeChanged(shooter.GrenadeChargeProgress);
            }
        }

        private void OnDisable()
        {
            if (shooter)
            {
                shooter.OnGrenadeChargeChanged -= HandleChargeChanged;
            }
        }

        private void LateUpdate()
        {
            UpdateVisibility();
        }

        private void HandleChargeChanged(float charge)
        {
            _currentCharge = Mathf.Clamp01(charge);

            if (fillImage)
            {
                fillImage.fillAmount = _currentCharge;
            }

            if (_currentCharge > 0.001f)
            {
                _lastVisibleTime = Time.time;
            }
        }

        private void UpdateVisibility()
        {
            if (!canvasGroup)
            {
                return;
            }

            float targetAlpha = 0f;

            if (_currentCharge > 0.001f || Time.time - _lastVisibleTime < hideDelay)
            {
                targetAlpha = 1f;
            }

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }
    }
}
