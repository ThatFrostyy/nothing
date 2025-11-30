using UnityEngine;

namespace WeaponUpgrades
{
    public abstract class WeaponUpgradeEffectBase : ScriptableObject, IWeaponUpgradeEffect
    {
        public abstract void Apply(Weapon weapon);
    }
}
