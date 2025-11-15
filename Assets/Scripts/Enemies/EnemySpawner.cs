using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FF
{
    [System.Serializable]
    public class WaveAttributeScaling
    {
        public AnimationCurve healthMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 4f);
        public AnimationCurve moveSpeedMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 1.6f);
        public AnimationCurve fireRateMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 1.5f);
        public AnimationCurve damageMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 2.2f);
        public AnimationCurve xpMultiplierByWave = AnimationCurve.Linear(1f, 1f, 20f, 2.5f);

        public EnemyWaveModifiers CreateModifiers(int wave)
        {
            return new EnemyWaveModifiers(
                Evaluate(healthMultiplierByWave, wave, 1f),
                Evaluate(moveSpeedMultiplierByWave, wave, 1f),
                Evaluate(fireRateMultiplierByWave, wave, 1f),
                Evaluate(damageMultiplierByWave, wave, 1f),
                Evaluate(xpMultiplierByWave, wave, 1f));
        }

        static float Evaluate(AnimationCurve curve, int wave, float fallback)
        {
            if (curve == null || curve.length == 0)
            {
                return fallback;
            }

            return Mathf.Max(0.01f, curve.Evaluate(Mathf.Max(1, wave)));
        }
    }

    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] GameObject[] enemyPrefabs;
        [SerializeField] GameObject[] bossPrefabs;
        [SerializeField] GameObject[] dogPrefabs;
        [SerializeField] Transform player;
        [SerializeField] float spawnRadius = 16f;
        [SerializeField] float spawnBuffer = 2f;
        [SerializeField] int maxSpawnAttempts = 8;
        [SerializeField] Camera spawnCamera;
        [SerializeField] AnimationCurve countByWave = AnimationCurve.Linear(1, 6, 20, 60);
        [SerializeField, Min(1)] int bossWaveInterval = 5;
        [SerializeField] AnimationCurve bossCountByWave = AnimationCurve.Linear(1, 1, 10, 3);
        [SerializeField] bool spawnRegularsDuringBossWave = true;
        [SerializeField, Min(0)] int maxUniqueRegularPrefabs = 0;
        [SerializeField, Min(0)] int maxUniqueBossPrefabs = 0;
        [SerializeField, Min(0)] int maxUniqueDogPrefabs = 0;
        [SerializeField] Vector2Int dogSpawnCountRange = new Vector2Int(1, 3);
        [SerializeField, Min(0)] int dogWaveStart = 5;
        [SerializeField, Min(1)] int dogWaveInterval = 3;
        [SerializeField, Range(0f, 1f)] float dogSpawnChance = 0.65f;
        [SerializeField] WaveAttributeScaling regularScaling = new WaveAttributeScaling();
        [SerializeField] WaveAttributeScaling bossScaling = new WaveAttributeScaling();
        [SerializeField] WaveAttributeScaling dogScaling = new WaveAttributeScaling();

        readonly List<GameObject> regularSelection = new List<GameObject>();
        readonly List<GameObject> bossSelection = new List<GameObject>();
        readonly List<GameObject> dogSelection = new List<GameObject>();
        readonly List<GameObject> scratchList = new List<GameObject>();

        void Awake()
        {
            if (!spawnCamera)
            {
                spawnCamera = Camera.main;
            }
        }

        public void SpawnWave(int wave)
        {
            if (!player) return;

            bool hasRegularPrefabs = enemyPrefabs != null && enemyPrefabs.Length > 0;
            bool hasBossPrefabs = bossPrefabs != null && bossPrefabs.Length > 0;
            bool hasDogPrefabs = dogPrefabs != null && dogPrefabs.Length > 0;

            bool isBossWave = hasBossPrefabs && bossWaveInterval > 0 && wave > 0 && wave % bossWaveInterval == 0;

            float radius = GetSpawnRadius();

            if (hasRegularPrefabs && (!isBossWave || spawnRegularsDuringBossWave))
            {
                int regularCount = EvaluateCount(countByWave, wave, 0);
                if (regularCount > 0)
                {
                    var prefabsToUse = GetPrefabSelection(enemyPrefabs, maxUniqueRegularPrefabs, regularSelection);
                    SpawnGroup(regularCount, radius, prefabsToUse, GetModifiers(regularScaling, wave));
                }
            }

            if (isBossWave)
            {
                int bossCount = EvaluateCount(bossCountByWave, wave, 1);
                if (bossCount > 0)
                {
                    var prefabsToUse = GetPrefabSelection(bossPrefabs, maxUniqueBossPrefabs, bossSelection);
                    SpawnGroup(bossCount, radius, prefabsToUse, GetModifiers(bossScaling, wave));
                }
            }

            if (hasDogPrefabs && ShouldSpawnDogs(wave))
            {
                int minDogs = Mathf.Max(0, Mathf.Min(dogSpawnCountRange.x, dogSpawnCountRange.y));
                int maxDogs = Mathf.Max(minDogs, Mathf.Max(dogSpawnCountRange.x, dogSpawnCountRange.y));
                int dogCount = Random.Range(minDogs, maxDogs + 1);
                if (dogCount > 0)
                {
                    var prefabsToUse = GetPrefabSelection(dogPrefabs, maxUniqueDogPrefabs, dogSelection);
                    SpawnGroup(dogCount, radius, prefabsToUse, GetModifiers(dogScaling, wave));
                }
            }
        }

        IList<GameObject> GetPrefabSelection(GameObject[] prefabs, int maxUnique, List<GameObject> selection)
        {
            selection.Clear();
            scratchList.Clear();

            if (prefabs == null || prefabs.Length == 0)
            {
                return selection;
            }

            for (int i = 0; i < prefabs.Length; i++)
            {
                var candidate = prefabs[i];
                if (candidate)
                {
                    scratchList.Add(candidate);
                }
            }

            if (scratchList.Count == 0)
            {
                return selection;
            }

            if (maxUnique <= 0 || maxUnique >= scratchList.Count)
            {
                selection.AddRange(scratchList);
            }
            else
            {
                int selectionCount = Mathf.Clamp(maxUnique, 1, scratchList.Count);
                for (int i = 0; i < selectionCount; i++)
                {
                    int index = Random.Range(0, scratchList.Count);
                    selection.Add(scratchList[index]);
                    scratchList.RemoveAt(index);
                }
            }

            return selection;
        }

        EnemyWaveModifiers GetModifiers(WaveAttributeScaling scaling, int wave)
        {
            return scaling != null ? scaling.CreateModifiers(wave) : EnemyWaveModifiers.Identity;
        }

        int EvaluateCount(AnimationCurve curve, int wave, int minimum)
        {
            if (curve == null || curve.length == 0)
            {
                return Mathf.Max(0, minimum);
            }

            int evaluated = Mathf.RoundToInt(curve.Evaluate(Mathf.Max(1, wave)));
            return Mathf.Max(minimum, evaluated);
        }

        bool ShouldSpawnDogs(int wave)
        {
            if (dogWaveInterval <= 0)
            {
                return false;
            }

            if (wave <= dogWaveStart)
            {
                return false;
            }

            if (((wave - dogWaveStart) % dogWaveInterval) != 0)
            {
                return false;
            }

            if (dogSpawnChance < 1f && Random.value > Mathf.Clamp01(dogSpawnChance))
            {
                return false;
            }

            return true;
        }

        void SpawnGroup(int count, float radius, IList<GameObject> prefabs, EnemyWaveModifiers modifiers)
        {
            if (prefabs == null || prefabs.Count == 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var prefab = prefabs[Random.Range(0, prefabs.Count)];
                if (!prefab)
                {
                    continue;
                }

                float a = Random.value * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 pos = FindSpawnPosition(direction, radius);

                var enemyInstance = Instantiate(prefab, pos, Quaternion.identity);
                if (enemyInstance.TryGetComponent<Enemy>(out var enemy))
                {
                    enemy.Initialize(player);
                    enemy.ApplyWaveModifiers(modifiers);
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

        void OnValidate()
        {
            dogSpawnCountRange.x = Mathf.Max(0, dogSpawnCountRange.x);
            dogSpawnCountRange.y = Mathf.Max(dogSpawnCountRange.x, dogSpawnCountRange.y);

            dogWaveInterval = Mathf.Max(1, dogWaveInterval);
            dogWaveStart = Mathf.Max(0, dogWaveStart);
            dogSpawnChance = Mathf.Clamp01(dogSpawnChance);
            maxUniqueRegularPrefabs = Mathf.Max(0, maxUniqueRegularPrefabs);
            maxUniqueBossPrefabs = Mathf.Max(0, maxUniqueBossPrefabs);
            maxUniqueDogPrefabs = Mathf.Max(0, maxUniqueDogPrefabs);
        }
    }
}
