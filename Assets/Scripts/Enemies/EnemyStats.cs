using UnityEngine;

namespace FF
{
    public class EnemyStats : MonoBehaviour, ICombatStats
    {
        public enum StatType { MoveSpeed, FireRate, Damage }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField, Range(0f, 1f)] private float acceleration = 0.18f;
        [SerializeField, Range(0f, 1f)] private float retreatSpeedMultiplier = 0.6f;
        [SerializeField] private float bodyTiltDegrees = 12f;

        [Header("Combat")]
        [SerializeField] private float shootingDistance = 10f;
        [SerializeField] private float distanceBuffer = 1.5f;
        [SerializeField] private float fireRateMultiplier = 1f;
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private float movementAccuracyPenalty = 1.3f;

        [Header("Avoidance")]
        [SerializeField] private float avoidanceRadius = 0.85f;
        [SerializeField] private float avoidancePush = 6f;
        [SerializeField, Range(0f, 1f)] private float avoidanceResponsiveness = 0.35f;
        [SerializeField, Range(0f, 2f)] private float avoidanceWeight = 0.75f;

        float baseMoveSpeed;
        float baseFireRateMultiplier;
        float baseDamageMultiplier;
        float baseMovementAccuracyPenalty;

        float currentMoveSpeed;
        float currentFireRateMultiplier;
        float currentDamageMultiplier;
        float currentMovementAccuracyPenalty;

        readonly System.Collections.Generic.List<TimedModifier> _activeModifiers = new();

        void Awake()
        {
            CacheBaseValues();
            ResetRuntimeValues();
        }

        public float MoveSpeed => currentMoveSpeed * GetActiveMultiplier(StatType.MoveSpeed);
        public float Acceleration => Mathf.Clamp01(acceleration);
        public float RetreatSpeedMultiplier => Mathf.Clamp01(retreatSpeedMultiplier);
        public float BodyTiltDegrees => bodyTiltDegrees;
        public float ShootingDistance => Mathf.Max(0f, shootingDistance);
        public float DistanceBuffer => Mathf.Max(0f, distanceBuffer);
        public float AvoidanceRadius => Mathf.Max(0f, avoidanceRadius);
        public float AvoidancePush => Mathf.Max(0f, avoidancePush);
        public float AvoidanceResponsiveness => Mathf.Clamp01(avoidanceResponsiveness);
        public float AvoidanceWeight => Mathf.Max(0f, avoidanceWeight);

        public float GetDamageMultiplier() => currentDamageMultiplier * GetActiveMultiplier(StatType.Damage);
        public float GetFireRateMultiplier() => currentFireRateMultiplier * GetActiveMultiplier(StatType.FireRate);
        public float GetMovementAccuracyPenalty() => currentMovementAccuracyPenalty;
        public float GetProjectileSpeedMultiplier() => 1f;
        public float GetFireCooldownMultiplier() => 1f;
        public float GetCritChance() => 0f;
        public float GetCritDamageMultiplier() => 1f;

        public void ApplyWaveMultipliers(float moveSpeedMultiplier, float fireRateMultiplierMultiplier, float damageMultiplierMultiplier, float accuracyPenaltyMultiplier)
        {
            CacheBaseValues();
            currentMoveSpeed = Mathf.Max(0f, baseMoveSpeed * Mathf.Max(0f, moveSpeedMultiplier));
            currentFireRateMultiplier = Mathf.Max(0f, baseFireRateMultiplier * Mathf.Max(0f, fireRateMultiplierMultiplier));
            currentDamageMultiplier = Mathf.Max(0f, baseDamageMultiplier * Mathf.Max(0f, damageMultiplierMultiplier));
            currentMovementAccuracyPenalty = Mathf.Max(0f, baseMovementAccuracyPenalty * Mathf.Max(0f, accuracyPenaltyMultiplier));
        }

        public void ResetRuntimeValues()
        {
            CacheBaseValues();
            currentMoveSpeed = baseMoveSpeed;
            currentFireRateMultiplier = baseFireRateMultiplier;
            currentDamageMultiplier = baseDamageMultiplier;
            currentMovementAccuracyPenalty = baseMovementAccuracyPenalty;

            _activeModifiers.Clear();
        }

        void CacheBaseValues()
        {
            baseMoveSpeed = Mathf.Max(0f, moveSpeed);
            baseFireRateMultiplier = Mathf.Max(0f, fireRateMultiplier);
            baseDamageMultiplier = Mathf.Max(0f, damageMultiplier);
            baseMovementAccuracyPenalty = Mathf.Max(0f, movementAccuracyPenalty);
        }

        void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            fireRateMultiplier = Mathf.Max(0f, fireRateMultiplier);
            damageMultiplier = Mathf.Max(0f, damageMultiplier);
            movementAccuracyPenalty = Mathf.Max(0f, movementAccuracyPenalty);

            if (!Application.isPlaying)
            {
                CacheBaseValues();
                ResetRuntimeValues();
            }
        }

        void Update()
        {
            UpdateActiveModifiers();
        }

        public void ApplyTemporaryMultiplier(StatType stat, float multiplier, float duration)
        {
            if (multiplier <= 0f)
            {
                return;
            }

            float expiry = duration > 0f ? Time.time + duration : float.PositiveInfinity;
            _activeModifiers.Add(new TimedModifier(stat, multiplier, expiry));
        }

        private void UpdateActiveModifiers()
        {
            if (_activeModifiers.Count == 0)
            {
                return;
            }

            float now = Time.time;
            for (int i = _activeModifiers.Count - 1; i >= 0; i--)
            {
                if (_activeModifiers[i].Expiry <= now)
                {
                    _activeModifiers.RemoveAt(i);
                }
            }
        }

        private float GetActiveMultiplier(StatType stat)
        {
            float value = 1f;
            for (int i = 0; i < _activeModifiers.Count; i++)
            {
                if (_activeModifiers[i].Stat == stat)
                {
                    value *= _activeModifiers[i].Multiplier;
                }
            }

            return value;
        }

        private readonly struct TimedModifier
        {
            public readonly StatType Stat;
            public readonly float Multiplier;
            public readonly float Expiry;

            public TimedModifier(StatType stat, float multiplier, float expiry)
            {
                Stat = stat;
                Multiplier = multiplier;
                Expiry = expiry;
            }
        }
    }
}
