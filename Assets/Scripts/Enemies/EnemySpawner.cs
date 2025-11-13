using UnityEngine;

namespace FF
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Transform player;
        [SerializeField] float spawnRadius = 16f;
        [SerializeField] float spawnBuffer = 2f;
        [SerializeField] int maxSpawnAttempts = 8;
        [SerializeField] Camera spawnCamera;
        [SerializeField] AnimationCurve countByWave = AnimationCurve.Linear(1, 6, 20, 60);

        void Awake()
        {
            if (!spawnCamera)
            {
                spawnCamera = Camera.main;
            }
        }

        public void SpawnWave(int wave)
        {
            if (!enemyPrefab || !player) return;

            int count = Mathf.RoundToInt(countByWave.Evaluate(wave));
            float radius = GetSpawnRadius();

            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 pos = FindSpawnPosition(direction, radius);

                var enemyInstance = Instantiate(enemyPrefab, pos, Quaternion.identity);
                if (enemyInstance.TryGetComponent<Enemy>(out var enemy))
                {
                    enemy.Initialize(player);
                }
            }
        }

        float GetSpawnRadius()
        {
            float radius = spawnRadius;
            float cameraRadius = GetCameraViewRadius();
            if (cameraRadius > 0f)
            {
                radius = Mathf.Max(radius, cameraRadius + spawnBuffer);
            }

            return radius;
        }

        Vector2 FindSpawnPosition(Vector2 direction, float baseRadius)
        {
            Vector2 origin = player ? (Vector2)player.position : Vector2.zero;
            float currentRadius = baseRadius;
            Vector2 candidate = origin + direction * currentRadius;

            int attemptsRemaining = Mathf.Max(1, maxSpawnAttempts);
            while (spawnCamera && IsPointVisible(candidate) && attemptsRemaining-- > 0)
            {
                currentRadius += spawnBuffer;
                candidate = origin + direction * currentRadius;
            }

            if (spawnCamera && IsPointVisible(candidate))
            {
                float minimumDistance = GetCameraViewRadius() + spawnBuffer;
                Vector2 cameraPosition = spawnCamera.transform.position;
                Vector2 cameraDirection = (candidate - cameraPosition).normalized;

                if (cameraDirection.sqrMagnitude < Mathf.Epsilon)
                {
                    cameraDirection = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector2.right;
                }

                candidate = cameraPosition + cameraDirection * minimumDistance;
            }

            return candidate;
        }

        bool IsPointVisible(Vector2 worldPosition)
        {
            if (!spawnCamera)
            {
                return false;
            }

            Vector3 viewportPoint = spawnCamera.WorldToViewportPoint(worldPosition);

            if (viewportPoint.z < 0f)
            {
                return false;
            }

            const float edgeTolerance = 0.01f;
            return viewportPoint.x > -edgeTolerance && viewportPoint.x < 1f + edgeTolerance &&
                   viewportPoint.y > -edgeTolerance && viewportPoint.y < 1f + edgeTolerance;
        }

        float GetCameraViewRadius()
        {
            if (!spawnCamera || !spawnCamera.orthographic)
            {
                return 0f;
            }

            float halfHeight = spawnCamera.orthographicSize;
            float halfWidth = halfHeight * spawnCamera.aspect;
            return Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
        }
    }
}
