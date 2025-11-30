using UnityEngine;
using UnityEngine.Audio;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
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

        [Header("Audio")]
        [SerializeField] private AudioClip flightLoopSFX;
        [SerializeField, Range(0f, 1f)] private float flightLoopVolume = 0.8f;

        [Header("Impact Behaviour")]
        [SerializeField] private bool explodeOnImpact = false;
        [SerializeField] private bool explodeOnLanding = false;

        [Header("Landing FX")]
        [SerializeField] private GameObject landingFX;
        [SerializeField] private AudioClip landingSFX;

        private static readonly Collider2D[] OverlapBuffer = new Collider2D[32];

        private Rigidbody2D _body;
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
        private Weapon _sourceWeapon;
        private bool _isArmed;
        private float _currentSpeed;
        private float _activeSlowdown;
        private Vector2 _movementDirection;
        private float _baseLaunchSpeed;
        private AudioSource _flightLoopSource;

        public int BaseDamage => baseDamage;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _poolToken = GetComponent<PoolToken>();
            if (!_poolToken)
            {
                _poolToken = gameObject.AddComponent<PoolToken>();
            }

            _flightLoopSource = GetComponent<AudioSource>();
            if (!_flightLoopSource)
            {
                _flightLoopSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigureFlightLoopSource();

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
            float? slowdownOverride = null,
            Weapon sourceWeapon = null)
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
            _sourceWeapon = sourceWeapon;

            float speed = Mathf.Max(0.1f, speedOverride ?? launchSpeed);
            if (_body)
            {
                _body.gravityScale = 0f;
                _movementDirection = direction.sqrMagnitude <= Mathf.Epsilon
                    ? Vector2.right
                    : direction.normalized;
                _currentSpeed = speed;
                _body.linearVelocity = _movementDirection * _currentSpeed;
            }

            StartFlightLoop();
        }

        private void Update()
        {
            if (_hasExploded || !_isArmed)
            {
                return;
            }

            _fuseTimer -= Time.deltaTime;
            if (_fuseTimer <= 0f)
            {
                Explode();
            }
        }

        private void FixedUpdate()
        {
            if (_hasExploded || !_isArmed || !_body)
            {
                return;
            }

            if (_currentSpeed > 0f)
            {
                _currentSpeed = Mathf.Max(0f, _currentSpeed - _activeSlowdown * Time.fixedDeltaTime);
                _body.linearVelocity = _movementDirection * _currentSpeed;
                if (_currentSpeed <= Mathf.Epsilon)
                {
                    _body.linearVelocity = Vector2.zero;
                    PlayLanding(transform.position);
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_hasExploded || _hasPlayedLanding || !_isArmed)
            {
                return;
            }

            int layer = collision.gameObject.layer;

            if (explodeOnImpact && IsLayerInMask(layer, damageLayers))
            {
                Explode();
                return;
            }

            if (!IsLayerInMask(layer, landingLayers))
            {
                return;
            }

            if (explodeOnLanding)
            {
                Explode();
            }
            else
            {
                Vector2 contactPoint = collision.contactCount > 0
                    ? collision.GetContact(0).point
                    : collision.transform.position;
                PlayLanding(contactPoint);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasExploded || !_isArmed)
            {
                return;
            }

            int layer = other.gameObject.layer;

            if (explodeOnImpact && IsLayerInMask(layer, damageLayers))
            {
                Explode();
                return;
            }

            if (explodeOnLanding && IsLayerInMask(layer, landingLayers))
            {
                Explode();
            }
        }

        private void PlayLanding(Vector3 point)
        {
            if (_hasPlayedLanding)
            {
                return;
            }

            _hasPlayedLanding = true;

            StopFlightLoop();

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

            StopFlightLoop();

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

            int hits = Physics2D.OverlapCircleNonAlloc(center, explosionRadius, OverlapBuffer, damageLayers);
            for (int i = 0; i < hits; i++)
            {
                Collider2D hit = OverlapBuffer[i];
                if (!hit)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(_ownerTag) && hit.CompareTag(_ownerTag))
                {
                    continue;
                }

                if (_pendingDamage > 0 && hit.TryGetComponent<Health>(out var health))
                {
                    health.Damage(_pendingDamage, _sourceWeapon);
                }

                Rigidbody2D hitBody = hit.attachedRigidbody;
                if (!hitBody)
                {
                    continue;
                }

                Vector2 offset = hitBody.position - center;
                if (offset.sqrMagnitude < 0.0001f)
                {
                    offset = Random.insideUnitCircle * 0.1f;
                }

                float distance = offset.magnitude;
                float normalized = Mathf.Clamp01(distance / Mathf.Max(0.01f, explosionRadius));
                float falloff = forceFalloff != null ? Mathf.Max(0f, forceFalloff.Evaluate(normalized)) : 1f - normalized;
                if (falloff <= 0f)
                {
                    continue;
                }

                Vector2 impulse = offset.normalized * (explosionForce * falloff);
                impulse.y += explosionForce * 0.15f * falloff;

                hitBody.AddForce(impulse * 2f, ForceMode2D.Impulse);
                if (hit.TryGetComponent<Enemy>(out var enemy))
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
            StopFlightLoop();
            if (_body)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }
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
            _sourceWeapon = null;
            StopFlightLoop();
            if (_body)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }
        }

        private void StartFlightLoop()
        {
            if (!_flightLoopSource || !flightLoopSFX)
            {
                return;
            }

            _flightLoopSource.outputAudioMixerGroup = _audioMixer;
            _flightLoopSource.clip = flightLoopSFX;
            _flightLoopSource.loop = true;
            _flightLoopSource.spatialBlend = Mathf.Clamp01(_audioSpatialBlend);
            _flightLoopSource.volume = Mathf.Clamp01(flightLoopVolume * _audioVolume);
            _flightLoopSource.pitch = _audioPitch;
            _flightLoopSource.Play();
        }

        private void StopFlightLoop()
        {
            if (_flightLoopSource)
            {
                _flightLoopSource.Stop();
            }
        }

        private void ConfigureFlightLoopSource()
        {
            if (!_flightLoopSource)
            {
                return;
            }

            _flightLoopSource.playOnAwake = false;
            _flightLoopSource.loop = true;
            _flightLoopSource.spatialBlend = 0f;
            _flightLoopSource.volume = Mathf.Clamp01(flightLoopVolume);
        }

        private static bool IsLayerInMask(int layer, LayerMask mask)
        {
            return ((1 << layer) & mask.value) != 0;
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
