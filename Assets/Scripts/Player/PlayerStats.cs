using UnityEngine;


namespace FF
{
    public class PlayerStats : MonoBehaviour, ICombatStats
    {
        public enum StatType { MoveSpeed, FireRate, Damage }

        [Header("Base Stats")]
        public float MoveSpeed = 6f;
        public float FireRateRPM = 450f;
        public float Damage = 10f;
        public float MovementAccuracyPenalty = 1.5f;


        [Header("Multipliers (from Upgrades)")]
        public float MoveMult = 1f;
        public float FireRateMult = 1f;
        public float DamageMult = 1f;

        private readonly System.Collections.Generic.List<TimedModifier> _activeModifiers = new();

        public float GetMoveSpeed() => MoveSpeed * MoveMult * GetActiveMultiplier(StatType.MoveSpeed);
        public float GetRPM() => FireRateRPM * FireRateMult * GetActiveMultiplier(StatType.FireRate);
        public int GetDamageInt() => Mathf.RoundToInt(Damage * DamageMult * GetActiveMultiplier(StatType.Damage));

        public float GetDamageMultiplier() => DamageMult * GetActiveMultiplier(StatType.Damage);
        public float GetFireRateMultiplier() => FireRateMult * GetActiveMultiplier(StatType.FireRate);
        public float GetMovementAccuracyPenalty() => MovementAccuracyPenalty;

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
