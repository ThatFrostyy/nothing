using UnityEngine;

namespace FF
{
    public interface ICombatStats
    {
        float GetDamageMultiplier();
        float GetFireRateMultiplier();
        float GetMovementAccuracyPenalty();
        float GetProjectileSpeedMultiplier();
        float GetFireCooldownMultiplier();
        float GetCritChance();
        float GetCritDamageMultiplier();
    }
}
