using UnityEngine;


namespace FF
{
    [RequireComponent(typeof(Collider2D))]
    public class XPOrb : MonoBehaviour
    {
        private const float DistanceEpsilon = 0.0001f;

        [SerializeField, Min(1)] private int value = 1;
        [SerializeField, Min(0f)] private float attractionRadius = 4f;
        [SerializeField, Min(0f)] private float moveSpeed = 10f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField] private AudioClip pickupSound;

        private Transform followTarget;
        private float currentSpeed;

        void Awake()
        {
            Collider2D collider = GetComponent<Collider2D>();
            if (collider)
            {
                collider.isTrigger = true;
            }
        }

        void Update()
        {
            AcquireTarget();
            MoveTowardsTarget(Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!TryGetWallet(other, out var wallet))
            {
                return;
            }

            wallet.Add(value);
            PlayPickupSound();
            Destroy(gameObject);
        }

        public void SetValue(int amount)
        {
            value = Mathf.Max(1, amount);
        }

        void AcquireTarget()
        {
            if (followTarget)
            {
                return;
            }

            var player = GameObject.FindWithTag("Player");
            if (!player)
            {
                return;
            }

            if (!player.TryGetComponent<XPWallet>(out _))
            {
                return;
            }

            followTarget = player.transform;
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
        }
    }
}
