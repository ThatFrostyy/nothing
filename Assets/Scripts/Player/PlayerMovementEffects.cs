using UnityEngine;

namespace FF
{
    public class PlayerMovementEffects : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private GameObject moveEffectPrefab;
        [SerializeField] private bool useMapSpecificEffect = true;

        [Header("Behaviour")]
        [SerializeField, Min(0f)] private float minSpeedForEffect = 0.5f;
        [SerializeField, Min(0f)] private float spawnInterval = 0.18f;
        [SerializeField] private bool alignToVelocity = true;
        [SerializeField, Min(0f)] private float backOffset = 0.35f;
        [SerializeField, Min(0)] private int poolPrewarmCount = 6;

        private float spawnTimer;
        private GameObjectPool moveEffectPool;
        private GameObject defaultMoveEffectPrefab;

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

            defaultMoveEffectPrefab = moveEffectPrefab;
            if (moveEffectPrefab)
            {
                moveEffectPool = PoolManager.GetPool(moveEffectPrefab, poolPrewarmCount, transform);
            }
        }

        private void OnEnable()
        {
            MapSelectionState.OnMapChanged += HandleMapChanged;
            ApplyMapEffect(MapSelectionState.SelectedMap);
        }

        private void OnDisable()
        {
            MapSelectionState.OnMapChanged -= HandleMapChanged;
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

        private void HandleMapChanged(MapDefinition map)
        {
            ApplyMapEffect(map);
        }

        private void ApplyMapEffect(MapDefinition map)
        {
            if (!useMapSpecificEffect)
            {
                return;
            }

            GameObject chosenPrefab = map && map.MovementEffectOverride
                ? map.MovementEffectOverride
                : defaultMoveEffectPrefab;

            if (chosenPrefab == moveEffectPrefab)
            {
                return;
            }

            moveEffectPrefab = chosenPrefab;
            if (moveEffectPrefab)
            {
                moveEffectPool = PoolManager.GetPool(moveEffectPrefab, poolPrewarmCount, transform);
            }
        }

        private void SpawnEffect(Vector2 velocity)
        {
            Vector3 position = spawnPoint ? spawnPoint.position : transform.position;
            if (velocity.sqrMagnitude > 0.0001f && backOffset > 0f)
            {
                Vector3 backward = (Vector3)velocity.normalized * backOffset;
                position -= backward;
            }

            Quaternion rotation = Quaternion.identity;
            if (alignToVelocity && velocity.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            if (moveEffectPrefab)
            {
                moveEffectPool = PoolManager.GetPool(moveEffectPrefab, poolPrewarmCount, transform);
            }

            if (moveEffectPool != null)
            {
                GameObject instance = moveEffectPool.Get(position, rotation);
                if (instance)
                {
                    if (!instance.TryGetComponent<PooledParticleSystem>(out var pooled))
                    {
                        pooled = instance.AddComponent<PooledParticleSystem>();
                        pooled.OnTakenFromPool();
                    }
                }
            }

            spawnTimer = spawnInterval;
        }
    }
}
