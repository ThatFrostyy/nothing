using UnityEngine;

namespace WeaponUpgrades
{
    public interface IWeaponUpgradeEffect
    {
        void Apply(Weapon weapon);
    }
}
