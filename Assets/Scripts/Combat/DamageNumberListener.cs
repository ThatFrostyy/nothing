using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Health))]
    public class DamageNumberListener : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health _health;
        [SerializeField] private Enemy _enemy;

        [Header("Emphasis")]
        [SerializeField, Range(0f, 1f)] private float _emphasizedThresholdFraction = 0.35f;
        [SerializeField, Min(0)] private int _minimumEmphasizedDamage = 10;

        private void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(DamageNumberListener)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
            }
        }

        private void OnValidate()
        {
            if (!_health) _health = GetComponent<Health>();
            if (!_enemy) _enemy = GetComponent<Enemy>();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
            }
        }

        private void HandleDamaged(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            bool emphasize = ShouldEmphasize(amount);
            DamageNumberManager.ShowDamage(transform.position, amount, emphasize);
        }

        private bool ShouldEmphasize(int amount)
        {
            if (_enemy != null && _enemy.IsBoss)
            {
                return true;
            }

            if (_health == null)
            {
                return false;
            }

            int emphasizedThreshold = Mathf.Max(_minimumEmphasizedDamage, Mathf.RoundToInt(_health.MaxHP * _emphasizedThresholdFraction));
            return amount >= emphasizedThreshold;
        }

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!_health)
            {
                Debug.LogError("Missing Health reference.", this);
                ok = false;
            }

            return ok;
        }
    }
}
