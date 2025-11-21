using UnityEngine;


namespace FF
{
    [CreateAssetMenu(menuName = "FF/Upgrade", fileName = "Upgrade_")]
    public class Upgrade : ScriptableObject
    {
        public string Title;
        [TextArea] public string Description;

        public enum Kind
        {
            DamageMult,
            FireRateMult,
            MoveMult,
            FireCooldownReduction,
            ProjectileSpeedMult,
            CritChance,
            CritDamageMult,
            MaxHealthMult
        }

        public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

        public Kind Type;
        public float Magnitude = 0.15f; // +15%
        [Min(0f)] public float Cap = 0f;
        [Min(0f)] public float MinValue = 0f;
        [Min(0)] public int MaxStacks = 0;
        [SerializeField] private Rarity rarity = Rarity.Common;
        [SerializeField, Min(0)] private int rarityWeightOverride = 0;


        public bool CanApply(PlayerStats stats, int timesTaken)
        {
            if (stats == null)
            {
                return false;
            }

            if (MaxStacks > 0 && timesTaken >= MaxStacks)
            {
                return false;
            }

            switch (Type)
            {
                case Kind.DamageMult: return !IsAtOrBeyondCap(stats.DamageMult, Cap);
                case Kind.FireRateMult: return !IsAtOrBeyondCap(stats.FireRateMult, Cap);
                case Kind.MoveMult: return !IsAtOrBeyondCap(stats.MoveMult, Cap);
                case Kind.FireCooldownReduction: return !IsBelowMin(stats.FireCooldownMult, MinValue > 0f ? MinValue : 0.1f);
                case Kind.ProjectileSpeedMult: return !IsAtOrBeyondCap(stats.ProjectileSpeedMult, Cap);
                case Kind.CritChance: return !IsAtOrBeyondCap(stats.CritChance, Cap > 0f ? Cap : 1f);
                case Kind.CritDamageMult: return !IsAtOrBeyondCap(stats.CritDamageMult, Cap);
                case Kind.MaxHealthMult: return stats.GetHealth() && !IsAtOrBeyondCap(stats.MaxHealthMult, Cap);
                default: return true;
            }
        }

        public void Apply(PlayerStats stats)
        {
            if (stats == null)
            {
                return;
            }

            switch (Type)
            {
                case Kind.DamageMult: stats.DamageMult += Magnitude; break;
                case Kind.FireRateMult: stats.FireRateMult += Magnitude; break;
                case Kind.MoveMult: stats.MoveMult += Magnitude; break;
                case Kind.FireCooldownReduction:
                    float minCooldown = MinValue > 0f ? MinValue : 0.1f;
                    stats.FireCooldownMult = Mathf.Max(minCooldown, stats.FireCooldownMult - Magnitude);
                    break;
                case Kind.ProjectileSpeedMult:
                    stats.ProjectileSpeedMult = ApplyCap(stats.ProjectileSpeedMult + Magnitude, Cap);
                    break;
                case Kind.CritChance:
                    float newCritChance = ApplyCap(stats.CritChance + Magnitude, Cap > 0f ? Cap : 1f);
                    stats.CritChance = Mathf.Clamp01(newCritChance);
                    break;
                case Kind.CritDamageMult:
                    stats.CritDamageMult = ApplyCap(stats.CritDamageMult + Magnitude, Cap);
                    break;
                case Kind.MaxHealthMult:
                    float newHealthMult = ApplyCap(stats.MaxHealthMult + Magnitude, Cap);
                    stats.MaxHealthMult = Mathf.Max(0f, newHealthMult);
                    if (stats.GetHealth())
                    {
                        stats.GetHealth().ScaleMaxHP(stats.MaxHealthMult, true);
                    }
                    break;
            }
        }

        public int GetWeight()
        {
            if (rarityWeightOverride > 0)
            {
                return rarityWeightOverride;
            }

            return rarity switch
            {
                Rarity.Common => 8,
                Rarity.Uncommon => 5,
                Rarity.Rare => 3,
                Rarity.Epic => 2,
                Rarity.Legendary => 1,
                _ => 1
            };
        }

        public Rarity GetRarity() => rarity;

        private static bool IsAtOrBeyondCap(float value, float cap)
        {
            return cap > 0f && value >= cap;
        }

        private static bool IsBelowMin(float value, float min)
        {
            return value <= min;
        }

        private static float ApplyCap(float value, float cap)
        {
            return cap > 0f ? Mathf.Min(value, cap) : value;
        }
    }
}
