using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class ShootCooldownBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AutoShooter shooter;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Image fillImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Positioning")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private float followLerpSpeed = 12f;

        [Header("Visibility")]
        [SerializeField] private float hideDelay = 0.35f;
        [SerializeField] private float fadeSpeed = 6f;

        private float _currentProgress = 1f;
        private float _lastActiveTime;

        private void Awake()
        {
            if (!shooter)
                shooter = GetComponentInParent<AutoShooter>();

            if (!followTarget && shooter)
                followTarget = shooter.transform;

            if (fillImage)
                fillImage.fillAmount = 1f;

            _currentProgress = shooter ? shooter.CooldownProgress : 1f;
        }

        private void OnEnable()
        {
            if (shooter)
            {
                shooter.OnCooldownChanged += HandleCooldownChanged;
                HandleCooldownChanged(shooter.CooldownProgress);
            }
        }

        private void OnDisable()
        {
            if (shooter)
                shooter.OnCooldownChanged -= HandleCooldownChanged;
        }

        private void LateUpdate()
        {
            UpdatePosition();
            UpdateVisibility();
        }

        private void UpdatePosition()
        {
            if (!followTarget)
                return;

            Vector3 targetPosition = followTarget.position + worldOffset;
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followLerpSpeed);
        }

        private void UpdateVisibility()
        {
            if (!canvasGroup)
                return;

            float targetAlpha = 1f;

            if (_currentProgress >= 0.999f && Time.time - _lastActiveTime >= hideDelay)
                targetAlpha = 0f;

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }

        private void HandleCooldownChanged(float progress)
        {
            _currentProgress = Mathf.Clamp01(progress);

            if (fillImage)
                fillImage.fillAmount = _currentProgress;

            if (_currentProgress < 0.999f)
                _lastActiveTime = Time.time;
        }
    }
}
