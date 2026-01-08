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


        [Header("Health")]
        [SerializeField] private Health health;
        [Min(0f)] public float MaxHealthMult = 1f;


        [Header("Multipliers (from Upgrades)")]
        public float MoveMult = 1f;
        public float FireRateMult = 1f;
        public float DamageMult = 1f;
        public float ProjectileSpeedMult = 1f;
        public float FireCooldownMult = 1f;
        [Range(0f, 1f)] public float CritChance = 0f;
        [Min(1f)] public float CritDamageMult = 1f;
        public float XPGatherRadius = 1f;
        public bool AdrenalineRush = false;
        public bool VampiricStrikes = false;
        public bool RicochetRounds = false;
        [Range(0f, 1f)] public float RicochetChance = 0.25f;

        private float _conditionalMoveMult = 1f;
        private float _conditionalFireRateMult = 1f;
        private float _conditionalDamageMult = 1f;
        private float _conditionalProjectileSpeedMult = 1f;

        private readonly System.Collections.Generic.List<TimedModifier> _activeModifiers = new();

        void Awake()
        {
            if (!health)
            {
                health = GetComponent<Health>();
            }
        }

        private void Start()
        {
            if (UpgradeManager.I != null)
                UpgradeManager.I.RegisterPlayerStats(this);
        }

        void OnValidate()
        {
            MoveSpeed = Mathf.Max(0f, MoveSpeed);
            FireRateRPM = Mathf.Max(0f, FireRateRPM);
            Damage = Mathf.Max(0f, Damage);
            MovementAccuracyPenalty = Mathf.Max(0f, MovementAccuracyPenalty);
            MaxHealthMult = Mathf.Max(0f, MaxHealthMult);
            MoveMult = Mathf.Max(0f, MoveMult);
            FireRateMult = Mathf.Max(0f, FireRateMult);
            DamageMult = Mathf.Max(0f, DamageMult);
            ProjectileSpeedMult = Mathf.Max(0f, ProjectileSpeedMult);
            FireCooldownMult = Mathf.Max(0.1f, FireCooldownMult);
            CritChance = Mathf.Clamp01(CritChance);
            CritDamageMult = Mathf.Max(1f, CritDamageMult);
            XPGatherRadius = Mathf.Max(0f, XPGatherRadius);
            RicochetChance = Mathf.Clamp01(RicochetChance);
        }

        public float GetMoveSpeed() => MoveSpeed * MoveMult * _conditionalMoveMult * GetActiveMultiplier(StatType.MoveSpeed);
        public float GetRPM() => FireRateRPM * FireRateMult * _conditionalFireRateMult * GetActiveMultiplier(StatType.FireRate);
        public int GetDamageInt() => Mathf.RoundToInt(Damage * DamageMult * _conditionalDamageMult * GetActiveMultiplier(StatType.Damage));

        public float GetDamageMultiplier() => DamageMult * _conditionalDamageMult * GetActiveMultiplier(StatType.Damage);
        public float GetFireRateMultiplier() => FireRateMult * _conditionalFireRateMult * GetActiveMultiplier(StatType.FireRate);
        public float GetMovementAccuracyPenalty() => MovementAccuracyPenalty;
        public float GetProjectileSpeedMultiplier() => ProjectileSpeedMult * _conditionalProjectileSpeedMult;
        public float GetFireCooldownMultiplier() => Mathf.Max(0.1f, FireCooldownMult);
        public float GetCritChance() => Mathf.Clamp01(CritChance);
        public float GetCritDamageMultiplier() => Mathf.Max(1f, CritDamageMult);
        public float GetXPGatherRadius() => XPGatherRadius;

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

        public void SetConditionalMoveMultiplier(float multiplier)
        {
            _conditionalMoveMult = Mathf.Max(0.01f, multiplier);
        }

        public void SetConditionalFireRateMultiplier(float multiplier)
        {
            _conditionalFireRateMult = Mathf.Max(0.01f, multiplier);
        }

        public void SetConditionalDamageMultiplier(float multiplier)
        {
            _conditionalDamageMult = Mathf.Max(0.01f, multiplier);
        }

        public void SetConditionalProjectileSpeedMultiplier(float multiplier)
        {
            _conditionalProjectileSpeedMult = Mathf.Max(0.01f, multiplier);
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

        public Health GetHealth() => health;
    }
}
