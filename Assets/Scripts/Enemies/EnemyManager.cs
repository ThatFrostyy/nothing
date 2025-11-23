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
        private readonly List<Enemy> _pendingRemoval = new();

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
            if (!_lookup.Contains(enemy))
            {
                return;
            }

            _pendingRemoval.Add(enemy);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            ProcessPendingRemovals();

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy enemy = _enemies[i];
                if (!enemy || !enemy.isActiveAndEnabled)
                {
                    _pendingRemoval.Add(enemy);
                    continue;
                }

                enemy.Tick(deltaTime);
            }

            ProcessPendingRemovals();
        }

        private void FixedUpdate()
        {
            float fixedDeltaTime = Time.fixedDeltaTime;
            ProcessPendingRemovals();

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy enemy = _enemies[i];
                if (!enemy || !enemy.isActiveAndEnabled)
                {
                    _pendingRemoval.Add(enemy);
                    continue;
                }

                enemy.PhysicsTick(fixedDeltaTime);
            }

            ProcessPendingRemovals();
        }

        private void ProcessPendingRemovals()
        {
            if (_pendingRemoval.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _pendingRemoval.Count; i++)
            {
                Enemy enemy = _pendingRemoval[i];
                _lookup.Remove(enemy);
                _enemies.Remove(enemy);
            }

            _pendingRemoval.Clear();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            _enemies.Clear();
            _lookup.Clear();
            _pendingRemoval.Clear();
        }
    }
}
