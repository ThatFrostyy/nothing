using System;
using System.Collections.Generic;
using UnityEngine;

namespace FF
{
    public interface IPoolable
    {
        void OnTakenFromPool();
        void OnReturnedToPool();
    }

    [DisallowMultipleComponent]
    public sealed class PoolToken : MonoBehaviour
    {
        internal GameObjectPool Owner { get; set; }

        public void Release()
        {
            if (Owner != null)
            {
                Owner.Release(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    public sealed class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Stack<GameObject> _available = new Stack<GameObject>();

        private static readonly List<IPoolable> PoolableCache = new List<IPoolable>(8);

        internal GameObjectPool(GameObject prefab, int initialCapacity, Transform parent)
        {
            _prefab = prefab ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;

            if (initialCapacity > 0)
            {
                Warmup(initialCapacity);
            }
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject instance = null;

            while (_available.Count > 0)
            {
                var candidate = _available.Pop();
                if (candidate != null) 
                {
                    instance = candidate;
                    break;
                }
            }

            if (instance == null)
            {
                instance = CreateInstance();
            }

            EnsureToken(instance);

            Transform t = instance.transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(position, rotation);

            if (!instance.activeSelf)
            {
                instance.SetActive(true);
            }

            NotifyTaken(instance);
            return instance;
        }

        public T GetComponent<T>(Vector3 position, Quaternion rotation) where T : Component
        {
            GameObject instance = Get(position, rotation);
            if (!instance.TryGetComponent(out T component))
            {
                component = instance.AddComponent<T>();
                if (component is IPoolable poolable)
                {
                    poolable.OnTakenFromPool();
                }
            }

            return component;
        }

        public void Release(GameObject instance)
        {
            if (!instance)
            {
                return;
            }

            PoolToken token = instance.GetComponent<PoolToken>();
            if (!token || token.Owner != this)
            {
                GameObject.Destroy(instance);
                return;
            }

            NotifyReturned(instance);

            if (instance.activeSelf)
            {
                instance.SetActive(false);
            }

            instance.transform.SetParent(_parent, false);
            _available.Push(instance);
        }

        private void Warmup(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject instance = CreateInstance();
                NotifyReturned(instance);
                if (instance.activeSelf)
                {
                    instance.SetActive(false);
                }

                instance.transform.SetParent(_parent, false);
                _available.Push(instance);
            }
        }

        internal void ClearDestroyedEntries()
        {
            if (_available.Count == 0) return;

            var valid = new Stack<GameObject>(_available.Count);
            foreach (var go in _available)
            {
                if (go != null)
                    valid.Push(go);
            }
            _available.Clear();
            foreach (var go in valid)
            {
                _available.Push(go);
            }
        }


        private GameObject CreateInstance()
        {
            GameObject instance = GameObject.Instantiate(_prefab, _parent);
            EnsureToken(instance);
            return instance;
        }

        private void EnsureToken(GameObject instance)
        {
            PoolToken token = instance.GetComponent<PoolToken>();
            if (!token)
            {
                token = instance.AddComponent<PoolToken>();
            }

            token.Owner = this;
        }

        private static void NotifyTaken(GameObject instance)
        {
            FillPoolableCache(instance);
            for (int i = 0; i < PoolableCache.Count; i++)
            {
                IPoolable poolable = PoolableCache[i];
                if (poolable != null)
                {
                    poolable.OnTakenFromPool();
                }
            }
        }

        private static void NotifyReturned(GameObject instance)
        {
            FillPoolableCache(instance);
            for (int i = 0; i < PoolableCache.Count; i++)
            {
                IPoolable poolable = PoolableCache[i];
                if (poolable != null)
                {
                    poolable.OnReturnedToPool();
                }
            }
        }

        private static void FillPoolableCache(GameObject instance)
        {
            PoolableCache.Clear();
            instance.GetComponents(PoolableCache);
        }
    }

    public static class PoolManager
    {
        private static readonly Dictionary<GameObject, GameObjectPool> Pools = new Dictionary<GameObject, GameObjectPool>();

        public static GameObjectPool GetPool(GameObject prefab, int initialCapacity = 0, Transform parent = null)
        {
            if (!prefab)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (!Pools.TryGetValue(prefab, out GameObjectPool pool))
            {
                pool = new GameObjectPool(prefab, initialCapacity, parent);
                Pools[prefab] = pool;
            }

            return pool;
        }

        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return GetPool(prefab).Get(position, rotation);
        }

        public static T GetComponent<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            if (!prefab)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            return GetPool(prefab.gameObject).GetComponent<T>(position, rotation);
        }

        public static void Release(GameObject instance)
        {
            if (!instance)
            {
                return;
            }

            PoolToken token = instance.GetComponent<PoolToken>();
            if (token != null && token.Owner != null)
            {
                token.Owner.Release(instance);
            }
            else
            {
                GameObject.Destroy(instance);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnDomainReload()
        {
            Pools.Clear();
        }

        static PoolManager()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += _ => ClearAll();
        }

        public static void ClearAll()
        {
            foreach (var kvp in Pools)
            {
                kvp.Value.ClearDestroyedEntries();
            }
            Pools.Clear();
        }

    }
}
