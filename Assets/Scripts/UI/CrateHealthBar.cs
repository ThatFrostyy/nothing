using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class CrateHealthBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health health;
        [SerializeField] private Image fillImage;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Transform followTarget;

        [Header("Appearance")]
        [SerializeField] private Vector3 worldOffset = new(0f, -0.75f, 0f);
        [SerializeField, Min(0f)] private float hideDelay = 0.35f;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 6f;

        float _lastVisibleTime;

        void Awake()
        {
            if (!health)
            {
                health = GetComponentInParent<Health>();
            }

            if (!followTarget && health)
            {
                followTarget = health.transform;
            }

            if (fillImage)
            {
                fillImage.fillAmount = 1f;
            }
        }

        void OnEnable()
        {
            if (health)
            {
                health.OnHealthChanged += HandleHealthChanged;
                HandleHealthChanged(health.CurrentHP, health.MaxHP);
            }
        }

        void OnDisable()
        {
            if (health)
            {
                health.OnHealthChanged -= HandleHealthChanged;
            }
        }

        void LateUpdate()
        {
            UpdatePosition();
            UpdateVisibility();
        }

        void HandleHealthChanged(int current, int max)
        {
            float fill = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            if (fillImage)
            {
                fillImage.fillAmount = fill;
            }

            if (fill < 0.999f)
            {
                _lastVisibleTime = Time.time;
            }
        }

        void UpdatePosition()
        {
            if (!followTarget)
            {
                return;
            }

            transform.position = followTarget.position + worldOffset;
        }

        void UpdateVisibility()
        {
            if (!canvasGroup)
            {
                return;
            }

            float targetAlpha = 0f;

            if (fillImage && fillImage.fillAmount < 0.999f)
            {
                targetAlpha = 1f;
            }
            else if (Time.time - _lastVisibleTime < hideDelay)
            {
                targetAlpha = 1f;
            }

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }
    }
}
