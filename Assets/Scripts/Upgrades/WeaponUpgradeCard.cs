using UnityEngine;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Weapon Upgrade Card", fileName = "WeaponUpgradeCard_")]
    public class WeaponUpgradeCard : ScriptableObject
    {
        [Header("Card Data")]
        public string Title = "Weapon Upgrade";
        [TextArea] public string DescriptionPrefix = "Boost weapon performance by ";

        [Header("Effect")]
        public WeaponUpgradeType Type = WeaponUpgradeType.Damage;
        [SerializeField] string percentColor = "#FFD966";

        public WeaponUpgradeOption BuildOption(Weapon weapon, float magnitude, int killCount, int cardsTaken)
        {
            string weaponName = GetWeaponName(weapon);
            int percentage = Mathf.RoundToInt(Mathf.Max(0f, magnitude) * 100f);

            string baseTitle = string.IsNullOrEmpty(Title) ? Type.ToString() : Title;
            string appendedTitle = string.IsNullOrEmpty(weaponName)
                ? baseTitle
                : $"{baseTitle} ({weaponName})";

            string coloredPercent = $"<color={percentColor}>+{percentage}%</color>.";
            string baseDescription = $"{DescriptionPrefix}{coloredPercent}";

            string extra = $"(Kills: {Mathf.Max(0, killCount)})";

            return new WeaponUpgradeOption(
                weapon,
                Type,
                magnitude,
                appendedTitle,
                baseDescription,
                appendedTitle,
                extra
            );
        }

        static string GetWeaponName(Weapon weapon)
        {
            if (weapon == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(weapon.weaponName))
            {
                return weapon.weaponName;
            }

            return weapon.name;
        }
    }
}
