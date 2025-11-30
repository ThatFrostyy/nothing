using System;
using UnityEngine;

namespace WeaponUpgrades
{
    public static class EnemyDeathEvents
    {
        public static event Action<Weapon, Enemy> OnEnemyKilled;

        public static void Raise(Weapon weapon, Enemy enemy)
        {
            OnEnemyKilled?.Invoke(weapon, enemy);
        }
    }

    public class Enemy : MonoBehaviour
    {
        // Placeholder class to show event usage.
    }
}
