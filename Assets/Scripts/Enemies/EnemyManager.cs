using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    [DefaultExecutionOrder(-50)]
    public class EnemyManager : MonoBehaviour
    {
        private static EnemyManager _instance;

        private readonly List<Enemy> _enemies = new();
        private readonly HashSet<Enemy> _lookup = new();

        public static void Register(Enemy enemy)
        {
            if (!enemy)
            {
                return;
            }

            EnemyManager instance = GetOrCreateInstance();
            instance?.InternalRegister(enemy);
        }

        public static void Unregister(Enemy enemy)
        {
            if (_instance == null || !enemy)
            {
                return;
            }

            _instance.InternalUnregister(enemy);
        }

        private static EnemyManager GetOrCreateInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindFirstObjectByType<EnemyManager>();
            if (_instance != null)
            {
                return _instance;
            }

            GameObject managerObject = new GameObject("EnemyManager");
            _instance = managerObject.AddComponent<EnemyManager>();
            DontDestroyOnLoad(managerObject);
            return _instance;
        }

        private void InternalRegister(Enemy enemy)
        {
            if (!_lookup.Add(enemy))
            {
                return;
            }

            _enemies.Add(enemy);
        }

        private void InternalUnregister(Enemy enemy)
        {
            if (!_lookup.Remove(enemy))
            {
                return;
            }

            _enemies.Remove(enemy);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            _enemies.Clear();
            _lookup.Clear();
        }
    }
}
