using System.Collections.Generic;
using UnityEngine;

namespace WeaponUpgrades
{
    [DisallowMultipleComponent]
    public class Weapon : MonoBehaviour
    {
        [Header("Weapon Stats")]
        [SerializeField] private string weaponId = "Weapon";
        [SerializeField] private float baseDamage = 10f;
        [SerializeField] private float fireRate = 1f;

        [Header("Upgrade Data")]
        [SerializeField] private WeaponUpgradeTree upgradeTree;
        [SerializeField] private List<int> experienceThresholds = new List<int> { 5, 15, 30, 60 };

        private float damageMultiplier = 1f;
        private float fireRateMultiplier = 1f;

        public string WeaponId => weaponId;
        public WeaponUpgradeTree UpgradeTree => upgradeTree;
        public IReadOnlyList<int> ExperienceThresholds => experienceThresholds;
        public float Damage => baseDamage * damageMultiplier;
        public float FireRate => fireRate * fireRateMultiplier;

        public void ApplyDamageMultiplier(float multiplier)
        {
            damageMultiplier *= multiplier;
        }

        public void ApplyFireRateMultiplier(float multiplier)
        {
            fireRateMultiplier *= multiplier;
        }
    }
}
