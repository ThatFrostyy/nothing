using UnityEngine;

namespace FF
{
    public enum WeaponUpgradeType
    {
        Damage,
        FireRate,
        ProjectileSpeed
    }

    [System.Serializable]
    public struct WeaponUpgradeOption
    {
        public Weapon Weapon;
        public WeaponUpgradeType Type;
        public float Magnitude;

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
            string finalDescription)
        {
            Weapon = weapon;
            Type = type;
            Magnitude = magnitude;

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

        public WeaponUpgradeState(Weapon weapon)
        {
            Weapon = weapon;
            CardsTaken = 0;
            damageBonus = 0f;
            fireRateBonus = 0f;
            projectileSpeedBonus = 0f;
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
            }
        }

        public float GetDamageMultiplier() => 1f + damageBonus;
        public float GetFireRateMultiplier() => 1f + fireRateBonus;
        public float GetProjectileSpeedMultiplier() => 1f + projectileSpeedBonus;
    }
}
