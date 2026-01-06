using System.Text.RegularExpressions;
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
            MaxHealthMult,
            Heal
        }

        public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

        public Kind Type;
        public float Magnitude = 0.15f; // +15%
        [Min(0f)] public float Cap = 0f;
        [Min(0f)] public float MinValue = 0f;
        [Min(0)] public int MaxStacks = 0;
        [Min(0)] public int HealAmount = 25;
        [Header("Wave Scaling")]
        [SerializeField] private bool startDoubledUntilWave = false;
        [SerializeField, Min(1)] private int normalizeValueByWave = 5;
        [SerializeField, Min(0)] private int unlockWave = 0;
        [Header("Feedback")]
        [SerializeField] private string levelUpPopupText = "";
        [SerializeField] private Rarity rarity = Rarity.Common;
        [SerializeField, Min(0)] private int rarityWeightOverride = 0;

        public int UnlockWave => unlockWave;
        public string LevelUpPopupText => levelUpPopupText;


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
                case Kind.Heal:
                    return stats.GetHealth() && HealAmount > 0 && stats.GetHealth().CurrentHP < stats.GetHealth().MaxHP;
                default: return true;
            }
        }

        public void Apply(PlayerStats stats)
        {
            if (stats == null)
            {
                return;
            }

            float appliedMagnitude = GetScaledMagnitude();
            int appliedHealAmount = GetScaledHealAmount();

            switch (Type)
            {
                case Kind.DamageMult: stats.DamageMult += appliedMagnitude; break;
                case Kind.FireRateMult:
                    stats.FireRateMult += appliedMagnitude;
                    if (UpgradeManager.I != null)
                    {
                        stats.FireRateMult = UpgradeManager.I.ClampFireRateMultiplier(stats.FireRateMult);
                    }
                    break;
                case Kind.MoveMult: stats.MoveMult += appliedMagnitude; break;
                case Kind.FireCooldownReduction:
                    float minCooldown = MinValue > 0f ? MinValue : 0.1f;
                    stats.FireCooldownMult = Mathf.Max(minCooldown, stats.FireCooldownMult - appliedMagnitude);
                    if (UpgradeManager.I != null)
                    {
                        stats.FireCooldownMult = UpgradeManager.I.ClampCooldownMultiplier(stats.FireCooldownMult);
                    }
                    break;
                case Kind.ProjectileSpeedMult:
                    stats.ProjectileSpeedMult = ApplyCap(stats.ProjectileSpeedMult + appliedMagnitude, Cap);
                    break;
                case Kind.CritChance:
                    float newCritChance = ApplyCap(stats.CritChance + appliedMagnitude, Cap > 0f ? Cap : 1f);
                    stats.CritChance = Mathf.Clamp01(newCritChance);
                    break;
                case Kind.CritDamageMult:
                    stats.CritDamageMult = ApplyCap(stats.CritDamageMult + appliedMagnitude, Cap);
                    break;
                case Kind.MaxHealthMult:
                    float newHealthMult = ApplyCap(stats.MaxHealthMult + appliedMagnitude, Cap);
                    stats.MaxHealthMult = Mathf.Max(0f, newHealthMult);
                    if (stats.GetHealth())
                    {
                        stats.GetHealth().ScaleMaxHP(stats.MaxHealthMult, false);
                    }
                    break;
                case Kind.Heal:
                    if (stats.GetHealth() && appliedHealAmount > 0)
                    {
                        stats.GetHealth().Heal(Mathf.Max(0, appliedHealAmount));
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

        public string GetPopupText()
        {
            if (!string.IsNullOrWhiteSpace(levelUpPopupText))
            {
                return levelUpPopupText;
            }

            return Type switch
            {
                Kind.DamageMult => "DAMAGE ↑",
                Kind.FireRateMult => "FIRE RATE ↑",
                Kind.MoveMult => "MOVE SPEED ↑",
                Kind.FireCooldownReduction => "COOLDOWN ↓",
                Kind.ProjectileSpeedMult => "PROJECTILE SPEED ↑",
                Kind.CritChance => "CRIT CHANCE ↑",
                Kind.CritDamageMult => "CRIT DAMAGE ↑",
                Kind.MaxHealthMult => "MAX HP ↑",
                Kind.Heal => "HEAL",
                _ => "UPGRADE ↑"
            };
        }

        public string GetDisplayDescription()
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                return Description;
            }

            string replacement = Type == Kind.Heal
                ? GetScaledHealAmount().ToString()
                : FormatPercentValue(GetScaledMagnitude() * 100f);

            return ReplaceFirstNumber(Description, replacement);
        }

        float GetScaledMagnitude()
        {
            if (!startDoubledUntilWave)
            {
                return Magnitude;
            }

            float multiplier = GetWaveScaleMultiplier();
            return Magnitude * multiplier;
        }

        int GetScaledHealAmount()
        {
            if (!startDoubledUntilWave)
            {
                return HealAmount;
            }

            float multiplier = GetWaveScaleMultiplier();
            return Mathf.RoundToInt(HealAmount * multiplier);
        }

        float GetWaveScaleMultiplier()
        {
            if (normalizeValueByWave <= 1)
            {
                return 1f;
            }

            int targetWave = Mathf.Max(1, normalizeValueByWave);
            int currentWave = 1;
            if (GameManager.I != null)
            {
                currentWave = Mathf.Max(1, GameManager.I.Wave);
            }

            float t = Mathf.InverseLerp(1f, targetWave, currentWave);
            return Mathf.Lerp(2f, 1f, t);
        }

        private static bool IsAtOrBeyondCap(float value, float cap)
        {
            return cap > 0f && value >= cap;
        }

        private static bool IsBelowMin(float value, float min)
        {
            return value <= min;
        }

        static string FormatPercentValue(float percent)
        {
            if (Mathf.Approximately(percent, Mathf.Round(percent)))
            {
                return Mathf.RoundToInt(percent).ToString();
            }

            return percent.ToString("0.#");
        }

        static string ReplaceFirstNumber(string text, string replacement)
        {
            return Regex.Replace(text, @"-?\d+(\.\d+)?", replacement, 1);
        }

        private static float ApplyCap(float value, float cap)
        {
            return cap > 0f ? Mathf.Min(value, cap) : value;
        }
    }
}
