using UnityEngine;

namespace FF
{
    public class PlayerMovementEffects : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private GameObject moveEffectPrefab;

        [Header("Behaviour")]
        [SerializeField, Min(0f)] private float minSpeedForEffect = 0.5f;
        [SerializeField, Min(0f)] private float spawnInterval = 0.18f;
        [SerializeField, Min(0f)] private float effectLifetime = 2.5f;
        [SerializeField] private bool alignToVelocity = true;

        private float spawnTimer;

        private void Awake()
        {
            if (!playerBody)
            {
                playerBody = GetComponentInParent<Rigidbody2D>();
            }

            if (!spawnPoint)
            {
                spawnPoint = transform;
            }
        }

        private void Update()
        {
            if (!moveEffectPrefab || !playerBody)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            spawnTimer = Mathf.Max(0f, spawnTimer - deltaTime);

            Vector2 velocity = playerBody.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < minSpeedForEffect)
            {
                return;
            }

            if (spawnTimer > 0f)
            {
                return;
            }

            SpawnEffect(velocity);
        }

        private void SpawnEffect(Vector2 velocity)
        {
            Vector3 position = spawnPoint ? spawnPoint.position : transform.position;
            Quaternion rotation = Quaternion.identity;
            if (alignToVelocity && velocity.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            var instance = Object.Instantiate(moveEffectPrefab, position, rotation);

            if (effectLifetime > 0f)
            {
                Object.Destroy(instance, effectLifetime);
            }

            spawnTimer = spawnInterval;
        }
    }
}
