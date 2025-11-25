using UnityEngine;

namespace FF
{
    [RequireComponent(typeof(Health))]
    public class EnemyDeathHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health _health;
        [SerializeField] private Enemy _enemy;
        [SerializeField] private AutoShooter _autoShooter;
        [SerializeField] private PoolToken _poolToken;

        private void Awake()
        {
            if (!ValidateDependencies())
            {
                Debug.LogError($"{nameof(EnemyDeathHandler)} on {name} disabled due to missing dependencies.", this);
                enabled = false;
            }
        }

        private void OnValidate()
        {
            if (!_health) _health = GetComponent<Health>();
            if (!_enemy) _enemy = GetComponent<Enemy>();
            if (!_autoShooter && _enemy) _autoShooter = _enemy.AutoShooter;
            if (!_poolToken) _poolToken = GetComponent<PoolToken>();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDeath -= HandleDeath;
            }
        }

        private void HandleDeath()
        {
            StopFiring();
            SpawnXPOrbs();
            RaiseDeathEvents();
            PlayDeathSound();
            SpawnDeathFx();
            ReleaseOrDestroy();
        }

        private void StopFiring()
        {
            if (_autoShooter != null)
            {
                _autoShooter.SetFireHeld(false);
            }
        }

        private void RaiseDeathEvents()
        {
            if (_enemy != null)
            {
                _enemy.RaiseKilled();
            }
        }

        private void PlayDeathSound()
        {
            if (_enemy == null)
            {
                return;
            }

            AudioClip clip = GetRandomClip(_enemy.DeathSounds);
            if (!clip)
            {
                return;
            }

            AudioSource source = _enemy.AudioSource;
            float volume = source ? source.volume : 1f;
            float pitch = source ? source.pitch : 1f;
            float spatialBlend = source ? source.spatialBlend : 0f;
            var mixerGroup = source ? source.outputAudioMixerGroup : null;

            AudioPlaybackPool.PlayOneShot(clip, transform.position, mixerGroup, spatialBlend, volume, pitch);
        }

        private void SpawnDeathFx()
        {
            if (_enemy == null)
            {
                return;
            }

            GameObject deathFx = _enemy.DeathFx;
            if (!deathFx)
            {
                return;
            }

            GameObject spawned = PoolManager.Get(deathFx, transform.position, Quaternion.identity);
            if (spawned && !spawned.TryGetComponent<PooledParticleSystem>(out var pooled))
            {
                pooled = spawned.AddComponent<PooledParticleSystem>();
                pooled.OnTakenFromPool();
            }
        }

        private void SpawnXPOrbs()
        {
            if (_enemy == null)
            {
                return;
            }

            XPOrb orbPrefab = _enemy.XpOrbPrefab;
            int orbValue = _enemy.XpOrbValue;
            int orbCount = _enemy.XpOrbCount;
            float spread = _enemy.XpOrbSpreadRadius;

            if (!orbPrefab || orbValue <= 0 || orbCount <= 0)
            {
                return;
            }

            GameObjectPool orbPool = PoolManager.GetPool(orbPrefab.gameObject, orbCount);
            for (int i = 0; i < orbCount; i++)
            {
                Vector3 spawnPosition = transform.position;
                if (spread > 0f)
                {
                    Vector2 offset = Random.insideUnitCircle * spread;
                    spawnPosition += (Vector3)offset;
                }

                XPOrb orb = orbPool.GetComponent<XPOrb>(spawnPosition, Quaternion.identity);
                if (orb)
                {
                    orb.SetValue(orbValue);
                }
            }
        }

        private void ReleaseOrDestroy()
        {
            if (_poolToken != null && _poolToken.Owner != null)
            {
                _poolToken.Release();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private AudioClip GetRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            int index = Random.Range(0, clips.Length);
            return clips[index];
        }

        private bool ValidateDependencies()
        {
            bool ok = true;

            if (!_health)
            {
                Debug.LogError("Missing Health reference.", this);
                ok = false;
            }

            if (!_enemy)
            {
                Debug.LogError("Missing Enemy reference.", this);
                ok = false;
            }

            return ok;
        }
    }
}
