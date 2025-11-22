using UnityEngine;
using UnityEngine.Audio;

namespace FF
{
    public class GrenadeProjectile : MonoBehaviour, IPoolable
    {
        [Header("Damage")]
        [SerializeField, Min(0)] private int baseDamage = 25;
        [SerializeField] private LayerMask damageLayers = ~0;

        [Header("Flight")]
        [SerializeField, Min(0.1f)] private float launchSpeed = 12f;
        [SerializeField, Min(0f)] private float slowdownRate = 15f;
        [SerializeField, Min(0.1f)] private float fuseDuration = 1.2f;
        [SerializeField] private LayerMask landingLayers = ~0;

        [Header("Explosion")]
        [SerializeField, Min(0.1f)] private float explosionRadius = 2.75f;
        [SerializeField, Min(0.1f)] private float explosionForce = 18f;
        [SerializeField] private AnimationCurve forceFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private GameObject explosionFX;
        [SerializeField] private AudioClip explosionSFX;

        [Header("Landing FX")]
        [SerializeField] private GameObject landingFX;
        [SerializeField] private AudioClip landingSFX;

        private PoolToken _poolToken;
        private float _fuseTimer;
        private bool _hasExploded;
        private bool _hasPlayedLanding;
        private int _pendingDamage;
        private string _ownerTag;
        private AudioMixerGroup _audioMixer;
        private float _audioSpatialBlend;
        private float _audioVolume = 1f;
        private float _audioPitch = 1f;
        private bool _isArmed;
        private float _currentSpeed;
        private float _activeSlowdown;
        private Vector2 _movementDirection;
        private float _baseLaunchSpeed;

        public int BaseDamage => baseDamage;

        private void Awake()
        {
            _poolToken = GetComponent<PoolToken>();
            if (!_poolToken)
            {
                _poolToken = gameObject.AddComponent<PoolToken>();
            }

            _baseLaunchSpeed = Mathf.Max(0.1f, launchSpeed);
        }

        public float BaseLaunchSpeed => _baseLaunchSpeed;

        public void Launch(
            Vector2 direction,
            int damageOverride = -1,
            float damageMultiplier = 1f,
            string ownerTag = null,
            AudioMixerGroup mixer = null,
            float spatialBlend = 0f,
            float volume = 1f,
            float pitch = 1f,
            float? fuseOverride = null,
            float? speedOverride = null,
            float? slowdownOverride = null)
        {
            int sourceDamage = damageOverride >= 0 ? damageOverride : baseDamage;
            _pendingDamage = Mathf.Max(0, Mathf.RoundToInt(sourceDamage * Mathf.Max(0f, damageMultiplier)));
            _ownerTag = ownerTag;
            _audioMixer = mixer;
            _audioSpatialBlend = spatialBlend;
            _audioVolume = Mathf.Clamp01(volume);
            _audioPitch = Mathf.Max(0.01f, pitch);
            _fuseTimer = Mathf.Max(0.05f, fuseOverride ?? fuseDuration);
            _isArmed = true;
            _hasExploded = false;
            _hasPlayedLanding = false;
            _activeSlowdown = Mathf.Max(0f, slowdownOverride ?? slowdownRate);

            float speed = Mathf.Max(0.1f, speedOverride ?? launchSpeed);
            _movementDirection = direction.sqrMagnitude <= Mathf.Epsilon
                ? Vector2.right
                : direction.normalized;
            _currentSpeed = speed;
        }

        private void Update()
        {
            if (_hasExploded || !_isArmed)
            {
                return;
            }

            MoveProjectile(Time.deltaTime);

            _fuseTimer -= Time.deltaTime;
            if (_fuseTimer <= 0f)
            {
                Explode();
            }
        }

        private void MoveProjectile(float deltaTime)
        {
            if (_currentSpeed > 0f)
            {
                transform.position += (Vector3)(_movementDirection * _currentSpeed * deltaTime);
                float nextSpeed = Mathf.Max(0f, _currentSpeed - _activeSlowdown * deltaTime);

                if (!_hasPlayedLanding && nextSpeed <= 0.05f)
                {
                    PlayLanding(transform.position);
                }

                _currentSpeed = nextSpeed;
            }
        }

        private void PlayLanding(Vector3 point)
        {
            if (_hasPlayedLanding)
            {
                return;
            }

            _hasPlayedLanding = true;

            if (landingFX)
            {
                GameObject fx = PoolManager.Get(landingFX, point, Quaternion.identity);
                if (fx && !fx.TryGetComponent<PooledParticleSystem>(out var pooled))
                {
                    pooled = fx.AddComponent<PooledParticleSystem>();
                    pooled.OnTakenFromPool();
                }
            }

            if (landingSFX)
            {
                AudioPlaybackPool.PlayOneShot(landingSFX, point, _audioMixer, _audioSpatialBlend, _audioVolume, _audioPitch);
            }
        }

        private void Explode()
        {
            if (_hasExploded)
            {
                return;
            }

            _hasExploded = true;
            Vector3 position = transform.position;

            if (explosionFX)
            {
                GameObject fx = PoolManager.Get(explosionFX, position, Quaternion.identity);
                if (fx && !fx.TryGetComponent<PooledParticleSystem>(out var pooled))
                {
                    pooled = fx.AddComponent<PooledParticleSystem>();
                    pooled.OnTakenFromPool();
                }
            }

            if (explosionSFX)
            {
                AudioPlaybackPool.PlayOneShot(explosionSFX, position, _audioMixer, _audioSpatialBlend, _audioVolume, _audioPitch);
            }

            ApplyExplosionDamage(position);
            ShockwaveUI.Trigger(position, Mathf.Clamp(explosionRadius / 4f, 0.35f, 1.5f));

            float shakeScale = Mathf.InverseLerp(1.5f, 4f, explosionRadius);
            float shakeDuration = Mathf.Lerp(0.15f, 0.32f, shakeScale);
            float shakeIntensity = Mathf.Lerp(0.2f, 0.45f, shakeScale);
            CameraShake.Shake(shakeDuration, shakeIntensity);

            if (_poolToken != null)
            {
                _poolToken.Release();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyExplosionDamage(Vector2 center)
        {
            if (explosionRadius <= 0f)
            {
                return;
            }

            Health[] targets = FindObjectsOfType<Health>();
            foreach (var health in targets)
            {
                if (!health)
                {
                    continue;
                }

                GameObject target = health.gameObject;
                if (((1 << target.layer) & damageLayers) == 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(_ownerTag) && target.CompareTag(_ownerTag))
                {
                    continue;
                }

                Vector2 offset = (Vector2)target.transform.position - center;
                float distance = offset.magnitude;
                if (distance > explosionRadius)
                {
                    continue;
                }

                if (_pendingDamage > 0)
                {
                    health.Damage(_pendingDamage);
                }

                float normalized = Mathf.Clamp01(distance / Mathf.Max(0.01f, explosionRadius));
                float falloff = forceFalloff != null ? Mathf.Max(0f, forceFalloff.Evaluate(normalized)) : 1f - normalized;
                if (falloff <= 0f)
                {
                    continue;
                }

                if (offset.sqrMagnitude < 0.0001f)
                {
                    offset = Random.insideUnitCircle * 0.1f;
                }

                Vector2 impulse = offset.normalized * (explosionForce * falloff);
                impulse.y += explosionForce * 0.15f * falloff;

                if (health.TryGetComponent<Enemy>(out var enemy))
                {
                    enemy.ApplyKnockback(impulse * 2f, 0.35f);
                }
            }
        }

        public void OnTakenFromPool()
        {
            _fuseTimer = fuseDuration;
            _hasExploded = false;
            _hasPlayedLanding = false;
            _isArmed = false;
            _currentSpeed = 0f;
            _movementDirection = Vector2.zero;
            _activeSlowdown = 0f;
        }

        public void OnReturnedToPool()
        {
            _pendingDamage = 0;
            _ownerTag = null;
            _hasExploded = false;
            _hasPlayedLanding = false;
            _fuseTimer = 0f;
            _isArmed = false;
            _currentSpeed = 0f;
            _movementDirection = Vector2.zero;
            _activeSlowdown = 0f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.25f);
            Gizmos.DrawSphere(transform.position, explosionRadius);
        }
#endif
    }
}
