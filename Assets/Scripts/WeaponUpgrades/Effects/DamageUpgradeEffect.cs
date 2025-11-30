using UnityEngine;

namespace WeaponUpgrades
{
    [CreateAssetMenu(menuName = "Upgrades/Effects/Damage Multiplier", fileName = "DamageUpgradeEffect")]
    public class DamageUpgradeEffect : WeaponUpgradeEffectBase
    {
        [SerializeField] private float damageMultiplier = 1.2f;

        public override void Apply(Weapon weapon)
        {
            if (weapon == null)
            {
                Debug.LogWarning("Tried to apply damage upgrade to a null weapon.");
                return;
            }

            weapon.ApplyDamageMultiplier(damageMultiplier);
        }
    }
}
