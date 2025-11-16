using UnityEngine;

namespace FF
{
    [DisallowMultipleComponent]
    public sealed class PooledParticleSystem : MonoBehaviour, IPoolable
    {
        [Header("Settings")]
        [SerializeField] private bool includeChildren = true;

        private ParticleSystem[] _particleSystems;
        private PoolToken _poolToken;
        private bool _isPlaying;

        private void Awake()
        {
            _poolToken = GetComponent<PoolToken>();
            if (!_poolToken)
            {
                _poolToken = gameObject.AddComponent<PoolToken>();
            }

            CacheParticleSystems();
            ConfigureStopActions();
        }

        private void Update()
        {
            if (!_isPlaying || _particleSystems == null || _particleSystems.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                var system = _particleSystems[i];
                if (system && system.IsAlive(true))
                {
                    return;
                }
            }

            _isPlaying = false;
            if (_poolToken != null)
            {
                _poolToken.Release();
            }
        }

        public void OnTakenFromPool()
        {
            if (_particleSystems == null || _particleSystems.Length == 0)
            {
                CacheParticleSystems();
            }

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                var system = _particleSystems[i];
                if (!system)
                {
                    continue;
                }

                system.Clear(true);
                system.Play(true);
            }

            _isPlaying = true;
        }

        public void OnReturnedToPool()
        {
            if (_particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                var system = _particleSystems[i];
                if (!system)
                {
                    continue;
                }

                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            _isPlaying = false;
        }

        private void CacheParticleSystems()
        {
            if (includeChildren)
            {
                _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }
            else
            {
                var system = GetComponent<ParticleSystem>();
                _particleSystems = system ? new[] { system } : System.Array.Empty<ParticleSystem>();
            }
        }

        private void ConfigureStopActions()
        {
            if (_particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                var system = _particleSystems[i];
                if (!system)
                {
                    continue;
                }

                var main = system.main;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }
    }
}
