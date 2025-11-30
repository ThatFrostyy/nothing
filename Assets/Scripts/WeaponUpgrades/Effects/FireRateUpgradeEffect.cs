using UnityEngine;

namespace WeaponUpgrades
{
    [CreateAssetMenu(menuName = "Upgrades/Effects/Fire Rate Multiplier", fileName = "FireRateUpgradeEffect")]
    public class FireRateUpgradeEffect : WeaponUpgradeEffectBase
    {
        [SerializeField] private float fireRateMultiplier = 1.15f;

        public override void Apply(Weapon weapon)
        {
            if (weapon == null)
            {
                Debug.LogWarning("Tried to apply fire rate upgrade to a null weapon.");
                return;
            }

            weapon.ApplyFireRateMultiplier(fireRateMultiplier);
        }
    }
}
