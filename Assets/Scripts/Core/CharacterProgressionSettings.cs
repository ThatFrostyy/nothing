using System;
using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    [Serializable]
    public class CharacterProgressionSettings
    {
        [Header("XP Requirements")]
        [SerializeField, Min(1)] private int baseXPRequirement = 50;
        [SerializeField, Min(0f)] private float xpGrowthPerLevel = 10f;
        [SerializeField] private AnimationCurve xpRequirementCurve;

        [Header("Run XP Gains")]
        [SerializeField, Min(0f)] private float xpPerKill = 0.5f;
        [SerializeField, Min(0f)] private float xpPerWave = 8f;
        [SerializeField, Min(0f)] private float waveKillBonusMultiplier = 0.05f;
        [SerializeField, Min(1)] private int minimumWaveForRewards = 1;

        [Header("Levels")]
        [SerializeField] private List<CharacterProgressionLevel> levels = new();

        public IReadOnlyList<CharacterProgressionLevel> Levels => levels;
        public int LevelCount => levels?.Count ?? 0;

        public int GetXPRequirementForLevel(int level)
        {
            if (level <= 0)
            {
                return 0;
            }

            if (levels != null && level - 1 < levels.Count)
            {
                CharacterProgressionLevel levelDef = levels[level - 1];
                if (levelDef.XPOverride > 0)
                {
                    return Mathf.Max(1, levelDef.XPOverride);
                }
            }

            if (xpRequirementCurve != null && xpRequirementCurve.length > 0)
            {
                float evaluated = xpRequirementCurve.Evaluate(level);
                if (evaluated > 0f)
                {
                    return Mathf.Max(1, Mathf.RoundToInt(evaluated));
                }
            }

            float requirement = baseXPRequirement + (level - 1) * xpGrowthPerLevel;
            return Mathf.Max(1, Mathf.RoundToInt(requirement));
        }

        public int CalculateRunXP(int highestWave, int totalKills)
        {
            if (highestWave < minimumWaveForRewards || totalKills <= 0)
            {
                return 0;
            }

            float waveXP = xpPerWave * Mathf.Max(0, highestWave);
            float killXP = xpPerKill * Mathf.Max(0, totalKills);
            float activityBonus = waveKillBonusMultiplier > 0f
                ? highestWave * totalKills * waveKillBonusMultiplier
                : 0f;

            float total = waveXP + killXP + activityBonus;
            return Mathf.Max(0, Mathf.RoundToInt(total));
        }

        public IEnumerable<CharacterUpgradeReward> GetUnlockedRewards(int currentLevel)
        {
            if (levels == null || levels.Count == 0 || currentLevel <= 0)
            {
                yield break;
            }

            for (int i = 0; i < levels.Count && i < currentLevel; i++)
            {
                CharacterProgressionLevel level = levels[i];
                if (level == null || level.Rewards == null)
                {
                    continue;
                }

                for (int r = 0; r < level.Rewards.Count; r++)
                {
                    CharacterUpgradeReward reward = level.Rewards[r];
                    if (reward != null)
                    {
                        yield return reward;
                    }
                }
            }
        }

        public CharacterProgressionSnapshot CreateSnapshot(CharacterProgressionState state)
        {
            int level = state?.Level ?? 0;
            int xp = state?.XP ?? 0;
            int nextRequirement = GetXPRequirementForLevel(Mathf.Clamp(level + 1, 1, Mathf.Max(LevelCount, 1)));

            return new CharacterProgressionSnapshot(level, xp, nextRequirement, levels);
        }
    }

    [Serializable]
    public class CharacterProgressionLevel
    {
        [Tooltip("If greater than zero, overrides the XP needed to reach this level.")]
        public int XPOverride = 0;
        public Sprite Icon;
        public string Description;
        public List<CharacterUpgradeReward> Rewards = new();
    }

    public enum CharacterUpgradeType
    {
        XPPickupSpeed,
        XPPickupRadius,
        MoveSpeed,
        AttackDamage,
        MaxHP,
        FireRate,
        DashCharge,
        ShortRangeDamage,
        DashFireRateBoost,
        MaxHPOnLevelUp,
        ShotgunPellets,
        SmgFireRateAndCooldown,
        DashImpactBlast
    }

    [Serializable]
    public class CharacterUpgradeReward
    {
        public CharacterUpgradeType Type;
        [Tooltip("Percent bonus applied permanently to this character (e.g. 5 = +5%).")]
        public float Percent = 5f;
        [Tooltip("Secondary percent value for upgrades that need two percentages.")]
        public float SecondaryPercent = 0f;
        [Tooltip("Flat value for upgrades that need a fixed amount (e.g. +1 dash charge).")]
        public int FlatAmount = 0;
        [Tooltip("Secondary flat value for upgrades that need two fixed amounts (e.g. max HP + heal).")]
        public int SecondaryFlatAmount = 0;
        [Tooltip("Duration in seconds for time-based rewards.")]
        public float DurationSeconds = 0f;
        [Tooltip("Radius used for area-based rewards.")]
        public float Radius = 0f;
        [Tooltip("Knockback force for impact-based rewards.")]
        public float KnockbackForce = 0f;
        [Tooltip("Knockback duration for impact-based rewards.")]
        public float KnockbackDuration = 0.25f;

        public float GetMultiplierDelta()
        {
            return Mathf.Max(0f, Percent) / 100f;
        }

        public float GetSecondaryMultiplierDelta()
        {
            return Mathf.Max(0f, SecondaryPercent) / 100f;
        }
    }

    public sealed class CharacterProgressionSnapshot
    {
        public readonly int Level;
        public readonly int XPInLevel;
        public readonly int XPToNext;
        public readonly IReadOnlyList<CharacterProgressionLevel> Levels;

        public CharacterProgressionSnapshot(
            int level,
            int xpInLevel,
            int xpToNext,
            IReadOnlyList<CharacterProgressionLevel> levels)
        {
            Level = Mathf.Max(0, level);
            XPInLevel = Mathf.Max(0, xpInLevel);
            XPToNext = Mathf.Max(1, xpToNext);
            Levels = levels;
        }
    }
}
