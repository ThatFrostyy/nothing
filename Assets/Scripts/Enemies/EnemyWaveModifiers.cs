using UnityEngine;

namespace FF
{
    /// <summary>
    /// Runtime multipliers applied to enemies as waves progress.
    /// </summary>
    public struct EnemyWaveModifiers
    {
        public static readonly EnemyWaveModifiers Identity = new EnemyWaveModifiers(1f, 1f, 1f, 1f, 1f);

        public float HealthMultiplier;
        public float MoveSpeedMultiplier;
        public float FireRateMultiplier;
        public float DamageMultiplier;
        public float XpValueMultiplier;

        public EnemyWaveModifiers(
            float healthMultiplier,
            float moveSpeedMultiplier,
            float fireRateMultiplier,
            float damageMultiplier,
            float xpValueMultiplier)
        {
            HealthMultiplier = Mathf.Max(0.01f, healthMultiplier);
            MoveSpeedMultiplier = Mathf.Max(0.01f, moveSpeedMultiplier);
            FireRateMultiplier = Mathf.Max(0.01f, fireRateMultiplier);
            DamageMultiplier = Mathf.Max(0.01f, damageMultiplier);
            XpValueMultiplier = Mathf.Max(0.01f, xpValueMultiplier);
        }
    }
}
