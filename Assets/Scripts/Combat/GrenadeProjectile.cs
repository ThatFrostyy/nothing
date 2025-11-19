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
        [SerializeField, Min(0f)] private float arcLift = 1.5f;
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

        public int BaseDamage => baseDamage;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _poolToken = GetComponent<PoolToken>();
            if (!_poolToken)
            {
                _poolToken = gameObject.AddComponent<PoolToken>();
            }
        }

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
            float? liftOverride = null)
        {
            int sourceDamage = damageOverride >= 0 ? damageOverride : baseDamage;
            _pendingDamage = Mathf.Max(0, Mathf.RoundToInt(sourceDamage * Mathf.Max(0f, damageMultiplier)));
            _ownerTag = ownerTag;
            _audioMixer = mixer;
            _audioSpatialBlend = spatialBlend;
            _audioVolume = Mathf.Clamp01(volume);
            _audioPitch = Mathf.Max(0.01f, pitch);
            _fuseTimer = Mathf.Max(0.05f, fuseOverride ?? fuseDuration);
            _hasExploded = false;
            _hasPlayedLanding = false;

            float speed = Mathf.Max(0.1f, speedOverride ?? launchSpeed);
            float lift = liftOverride ?? arcLift;
            if (_body)
            {
                Vector2 velocity = direction.sqrMagnitude <= Mathf.Epsilon
                    ? Vector2.right * speed
                    : direction.normalized * speed;
                velocity.y += lift;
                _body.linearVelocity = velocity;
            }
        }

        private void Update()
        {
            if (_hasExploded)
            {
                return;
            }

            _fuseTimer -= Time.deltaTime;
            if (_fuseTimer <= 0f)
            {
                Explode();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_hasExploded || _hasPlayedLanding)
            {
                return;
            }

            if (((1 << collision.gameObject.layer) & landingLayers.value) == 0)
            {
                return;
            }

            _hasPlayedLanding = true;
            Vector3 point = collision.GetContact(0).point;

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
            CameraShake.Shake(0.18f, 0.25f);

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
                    health.Damage(_pendingDamage);
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
                hitBody.linearVelocity += impulse;
            }
        }

        public void OnTakenFromPool()
        {
            _fuseTimer = fuseDuration;
            _hasExploded = false;
            _hasPlayedLanding = false;
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
            if (_body)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }
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
