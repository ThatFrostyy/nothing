using System.Collections;
using UnityEngine;


namespace FF
{
    [RequireComponent(typeof(Collider2D))]
    public class XPOrb : MonoBehaviour, IPoolable
    {
        private const float DistanceEpsilon = 0.0001f;

        [Header("Orb Settings")]
        [SerializeField, Min(1)] private int value = 1;
        [SerializeField, Min(0f)] private float attractionRadius = 4f;
        [SerializeField, Min(0f)] private float moveSpeed = 10f;
        [SerializeField, Min(0f)] private float acceleration = 18f;

        [Header("Audio & Visual")]
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private AudioSource pickupAudioSource;
        [SerializeField] private Vector2 pulseScaleRange = new(0.9f, 1.1f);
        [SerializeField, Min(0f)] private float pulseSpeed = 4f;
        [SerializeField] private bool randomizePulseOffset = true;

        [Header("Lifetime")]
        [SerializeField, Min(0f)] private float lifetime = 20f;
        [SerializeField, Min(0f)] private float lifetimeVariance = 0f;

        private Transform followTarget;
        private float currentSpeed;
        private PoolToken poolToken;
        private Vector3 baseScale;
        private float pulseTimer;
        private static Transform cachedPlayerTransform;
        private static XPWallet cachedWallet;
        private Collider2D orbCollider;
        private Renderer[] cachedRenderers;
        private bool collected;
        private Coroutine releaseRoutine;
        private float lifetimeTimer;
        private float currentLifetime;

        void Awake()
        {
            orbCollider = GetComponent<Collider2D>();
            if (orbCollider)
            {
                orbCollider.isTrigger = true;
            }

            poolToken = GetComponent<PoolToken>();
            if (!poolToken)
            {
                poolToken = gameObject.AddComponent<PoolToken>();
            }

            if (!pickupAudioSource)
            {
                pickupAudioSource = GetComponent<AudioSource>();
            }

            if (!pickupAudioSource)
            {
                pickupAudioSource = gameObject.AddComponent<AudioSource>();
            }

            if (pickupAudioSource)
            {
                pickupAudioSource.playOnAwake = false;
                pickupAudioSource.loop = false;
                pickupAudioSource.spatialBlend = 0f;
                pickupAudioSource.ignoreListenerPause = true;
            }

            baseScale = transform.localScale;
            ResetPulseTimer();
            CacheRenderers();
        }

        void Update()
        {
            if (collected)
            {
                return;
            }

            UpdateLifetime(Time.deltaTime);
            if (collected)
            {
                return;
            }
            AcquireTarget();
            MoveTowardsTarget(Time.deltaTime);
            AnimatePulse(Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!TryGetWallet(other, out var wallet))
            {
                return;
            }

            wallet.Add(value);
            HandleCollected();
        }

        public void SetValue(int amount)
        {
            value = Mathf.Max(1, amount);
        }

        public void OnTakenFromPool()
        {
            ResetOrbState();
        }

        public void OnReturnedToPool()
        {
            ResetOrbState();
        }

        void AcquireTarget()
        {
            if (followTarget)
            {
                return;
            }

            if (cachedPlayerTransform && cachedWallet && cachedPlayerTransform.gameObject.activeInHierarchy)
            {
                followTarget = cachedPlayerTransform;
                return;
            }

            if (!cachedPlayerTransform || !cachedWallet)
            {
                CachePlayerWallet();
            }

            if (!cachedPlayerTransform)
            {
                return;
            }

            followTarget = cachedPlayerTransform;
        }

        void MoveTowardsTarget(float deltaTime)
        {
            if (!followTarget)
            {
                return;
            }

            Vector3 toTarget = followTarget.position - transform.position;
            float sqrDistance = toTarget.sqrMagnitude;

            if (attractionRadius <= 0f || sqrDistance <= 0f)
            {
                return;
            }

            float radiusSqr = attractionRadius * attractionRadius;
            if (sqrDistance > radiusSqr)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * deltaTime);
                return;
            }

            float distance = Mathf.Sqrt(sqrDistance);
            if (distance <= DistanceEpsilon)
            {
                return;
            }

            Vector3 direction = toTarget / distance;
            currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, acceleration * deltaTime);
            transform.position += currentSpeed * deltaTime * direction;
        }

        void AnimatePulse(float deltaTime)
        {
            if (pulseSpeed <= 0f)
            {
                return;
            }

            float min = Mathf.Min(pulseScaleRange.x, pulseScaleRange.y);
            float max = Mathf.Max(pulseScaleRange.x, pulseScaleRange.y);
            if (Mathf.Approximately(max, 0f))
            {
                return;
            }

            float amplitude = Mathf.Max(0f, (max - min) * 0.5f);
            float midpoint = min + amplitude;

            pulseTimer += deltaTime * pulseSpeed;
            float scaleMultiplier = midpoint + Mathf.Sin(pulseTimer) * amplitude;
            transform.localScale = baseScale * Mathf.Max(scaleMultiplier, 0.01f);
        }

        bool TryGetWallet(Collider2D other, out XPWallet wallet)
        {
            wallet = other.GetComponent<XPWallet>();
            if (!wallet)
            {
                wallet = other.GetComponentInParent<XPWallet>();
            }

            return wallet != null;
        }

        float PlayPickupSound()
        {
            if (!pickupSound)
            {
                return 0f;
            }

            if (pickupAudioSource)
            {
                pickupAudioSource.PlayOneShot(pickupSound, GameAudioSettings.SfxVolume);
                return pickupSound.length / Mathf.Max(0.01f, pickupAudioSource.pitch);
            }

            AudioSource.PlayClipAtPoint(pickupSound, transform.position, GameAudioSettings.SfxVolume);
            return pickupSound.length;
        }

        void OnValidate()
        {
            value = Mathf.Max(1, value);
            attractionRadius = Mathf.Max(0f, attractionRadius);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            pulseScaleRange.x = Mathf.Max(0f, pulseScaleRange.x);
            pulseScaleRange.y = Mathf.Max(0f, pulseScaleRange.y);
            lifetime = Mathf.Max(0f, lifetime);
            lifetimeVariance = Mathf.Max(0f, lifetimeVariance);
        }

        void ResetPulseTimer()
        {
            if (randomizePulseOffset)
            {
                pulseTimer = Random.Range(0f, Mathf.PI * 2f);
            }
            else
            {
                pulseTimer = 0f;
            }
        }

        void HandleCollected()
        {
            if (collected)
            {
                return;
            }

            PrepareForRelease(false);

            float wait = PlayPickupSound();
            BeginRelease(wait);
        }

        void HandleExpired()
        {
            if (collected)
            {
                return;
            }

            PrepareForRelease(true);
            BeginRelease(0f);
        }

        void ResetOrbState()
        {
            if (releaseRoutine != null)
            {
                StopCoroutine(releaseRoutine);
                releaseRoutine = null;
            }

            collected = false;
            followTarget = null;
            currentSpeed = 0f;

            if (pickupAudioSource)
            {
                pickupAudioSource.Stop();
            }

            if (orbCollider)
            {
                orbCollider.enabled = true;
            }

            SetRenderersEnabled(true);
            transform.localScale = baseScale;
            ResetPulseTimer();
            lifetimeTimer = 0f;
            currentLifetime = GetLifetimeDuration();
        }

        void UpdateLifetime(float deltaTime)
        {
            if (collected)
            {
                return;
            }

            if (currentLifetime <= 0f)
            {
                return;
            }

            lifetimeTimer += deltaTime;
            if (lifetimeTimer >= currentLifetime)
            {
                HandleExpired();
            }
        }

        float GetLifetimeDuration()
        {
            if (lifetime <= 0f)
            {
                return 0f;
            }

            float variance = lifetimeVariance > 0f ? Random.Range(-lifetimeVariance, lifetimeVariance) : 0f;
            float duration = lifetime + variance;
            return Mathf.Max(0f, duration);
        }

        void PrepareForRelease(bool stopAudio)
        {
            collected = true;
            followTarget = null;
            currentSpeed = 0f;

            if (stopAudio && pickupAudioSource)
            {
                pickupAudioSource.Stop();
            }

            if (orbCollider)
            {
                orbCollider.enabled = false;
            }

            SetRenderersEnabled(false);
        }

        void BeginRelease(float delay)
        {
            if (releaseRoutine != null)
            {
                StopCoroutine(releaseRoutine);
            }

            releaseRoutine = StartCoroutine(ReleaseAfterDelay(delay));
        }

        IEnumerator ReleaseAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (poolToken != null)
            {
                poolToken.Release();
            }

            releaseRoutine = null;
        }

        void CacheRenderers()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void SetRenderersEnabled(bool enabled)
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                CacheRenderers();
            }

            for (int i = 0; i < (cachedRenderers?.Length ?? 0); i++)
            {
                var renderer = cachedRenderers[i];
                if (renderer)
                {
                    renderer.enabled = enabled;
                }
            }
        }

        static void CachePlayerWallet()
        {
            cachedPlayerTransform = null;
            cachedWallet = null;

            var player = GameObject.FindWithTag("Player");
            if (!player)
            {
                return;
            }

            cachedWallet = player.GetComponent<XPWallet>();
            if (!cachedWallet)
            {
                cachedWallet = player.GetComponentInParent<XPWallet>();
            }

            cachedPlayerTransform = cachedWallet ? cachedWallet.transform : null;
        }
    }
}
