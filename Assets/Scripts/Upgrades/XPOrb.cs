using UnityEngine;


namespace FF
{
    [RequireComponent(typeof(Collider2D))]
    public class XPOrb : MonoBehaviour, IPoolable
    {
        private const float DistanceEpsilon = 0.0001f;

        [SerializeField, Min(1)] private int value = 1;
        [SerializeField, Min(0f)] private float attractionRadius = 4f;
        [SerializeField, Min(0f)] private float moveSpeed = 10f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private Vector2 pulseScaleRange = new Vector2(0.9f, 1.1f);
        [SerializeField, Min(0f)] private float pulseSpeed = 4f;
        [SerializeField] private bool randomizePulseOffset = true;

        private Transform followTarget;
        private float currentSpeed;
        private PoolToken poolToken;
        private Vector3 baseScale;
        private float pulseTimer;
        private static Transform cachedPlayerTransform;
        private static XPWallet cachedWallet;

        void Awake()
        {
            Collider2D collider = GetComponent<Collider2D>();
            if (collider)
            {
                collider.isTrigger = true;
            }

            poolToken = GetComponent<PoolToken>();
            if (!poolToken)
            {
                poolToken = gameObject.AddComponent<PoolToken>();
            }

            baseScale = transform.localScale;
            ResetPulseTimer();
        }

        void Update()
        {
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
            PlayPickupSound();
            if (poolToken != null)
            {
                poolToken.Release();
            }
        }

        public void SetValue(int amount)
        {
            value = Mathf.Max(1, amount);
        }

        public void OnTakenFromPool()
        {
            followTarget = null;
            currentSpeed = 0f;
            transform.localScale = baseScale;
            ResetPulseTimer();
        }

        public void OnReturnedToPool()
        {
            followTarget = null;
            currentSpeed = 0f;
            transform.localScale = baseScale;
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

        void PlayPickupSound()
        {
            if (!pickupSound)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        void OnValidate()
        {
            value = Mathf.Max(1, value);
            attractionRadius = Mathf.Max(0f, attractionRadius);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            pulseScaleRange.x = Mathf.Max(0f, pulseScaleRange.x);
            pulseScaleRange.y = Mathf.Max(0f, pulseScaleRange.y);
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
