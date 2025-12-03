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

            if (canvasGroup)
            {
                canvasGroup.alpha = 1f;
                _lastVisibleTime = Time.time;
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
            if (!canvasGroup) return;
            canvasGroup.alpha = 1f;  
        }

    }
}
