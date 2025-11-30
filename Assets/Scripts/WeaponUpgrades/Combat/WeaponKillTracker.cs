using UnityEngine;

namespace WeaponUpgrades
{
    [DisallowMultipleComponent]
    public class WeaponKillTracker : MonoBehaviour
    {
        [SerializeField] private WeaponUpgradeManager upgradeManager;

        private void OnEnable()
        {
            EnemyDeathEvents.OnEnemyKilled += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            EnemyDeathEvents.OnEnemyKilled -= HandleEnemyKilled;
        }

        private void HandleEnemyKilled(Weapon weapon, Enemy enemy)
        {
            if (weapon == null || upgradeManager == null)
            {
                return;
            }

            upgradeManager.AddExperience(weapon, 1);
        }
    }
}
