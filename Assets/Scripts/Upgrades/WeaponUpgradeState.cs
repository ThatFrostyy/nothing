using UnityEngine;

namespace FF
{
    public enum WeaponUpgradeType
    {
        Damage,
        FireRate,
        ProjectileSpeed,
        Pierce,
        ExtraProjectiles,
        FireCooldownReduction
    }

    [System.Serializable]
    public struct WeaponUpgradeOption
    {
        public Weapon Weapon;
        public WeaponUpgradeType Type;
        public float Magnitude;
        public Upgrade.Rarity Rarity;

        public string BaseTitle;       // YOU fill this in inspector or code
        public string BaseDescription; // YOU fill this
        public string FinalTitle;      // auto-generated output
        public string FinalDescription;

        public WeaponUpgradeOption(
            Weapon weapon,
            WeaponUpgradeType type,
            float magnitude,
            string baseTitle,
            string baseDescription,
            string finalTitle,
            string finalDescription,
            Upgrade.Rarity rarity)
        {
            Weapon = weapon;
            Type = type;
            Magnitude = magnitude;
            Rarity = rarity;

            BaseTitle = baseTitle;
            BaseDescription = baseDescription;
            FinalTitle = finalTitle;
            FinalDescription = finalDescription;
        }
    }


    public class WeaponUpgradeState
    {
        public Weapon Weapon { get; }
        public int CardsTaken { get; private set; }

        float damageBonus;
        float fireRateBonus;
        float projectileSpeedBonus;
        int pierceCount;
        int extraProjectiles;
        float fireCooldownReduction;

        public WeaponUpgradeState(Weapon weapon)
        {
            Weapon = weapon;
            CardsTaken = 0;
            damageBonus = 0f;
            fireRateBonus = 0f;
            projectileSpeedBonus = 0f;
            pierceCount = 0;
            extraProjectiles = 0;
            fireCooldownReduction = 0f;
        }

        public void Apply(WeaponUpgradeOption option)
        {
            CardsTaken++;
            float amount = Mathf.Max(0f, option.Magnitude);

            switch (option.Type)
            {
                case WeaponUpgradeType.Damage:
                    damageBonus += amount;
                    break;
                case WeaponUpgradeType.FireRate:
                    fireRateBonus += amount;
                    break;
                case WeaponUpgradeType.ProjectileSpeed:
                    projectileSpeedBonus += amount;
                    break;
                case WeaponUpgradeType.Pierce:
                    pierceCount += Mathf.RoundToInt(amount);
                    break;
                case WeaponUpgradeType.ExtraProjectiles:
                    extraProjectiles += Mathf.RoundToInt(amount);
                    break;
                case WeaponUpgradeType.FireCooldownReduction:
                    fireCooldownReduction += amount;
                    break;
            }
        }

        public float GetDamageMultiplier() => 1f + damageBonus;
        public float GetFireRateMultiplier() => 1f + fireRateBonus;
        public float GetProjectileSpeedMultiplier() => 1f + projectileSpeedBonus;
        public int GetPierceCount() => pierceCount;
        public int GetExtraProjectiles() => extraProjectiles;
        public float GetFireCooldownMultiplier() => Mathf.Max(0.1f, 1f - fireCooldownReduction);
    }
}
