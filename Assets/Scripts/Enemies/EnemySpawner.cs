using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FF
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Player & Spawn Area")]
        [SerializeField] private Transform player;
        [SerializeField, Min(1f)] private float spawnRadius = 16f;
        [SerializeField, Min(0f)] private float spawnBuffer = 2f;
        [SerializeField, Min(1)] private int maxSpawnAttempts = 8;
        [SerializeField] private Camera spawnCamera;

        [Header("Waves")]
        [SerializeField, Min(1)] private int bossWaveInterval = 5;
        [SerializeField] private bool spawnNonBossDefinitionsDuringBossWave = true;
        [SerializeField] private WaveAttributeScaling defaultScaling = new();
        [SerializeField] private List<EnemySpawnDefinition> spawnDefinitions = new();

        [Header("Limits (0 = Unlimited)")]
        [SerializeField, Min(0)] private int maxActiveNonBosses = 0;
        [SerializeField, Min(0)] private int maxActiveBosses = 0;
        [SerializeField, Min(0)] private int maxActiveTotal = 0;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip fallbackSpawnClip;

        private int _activeBosses;
        private int _activeNonBosses;

        private void Awake()
        {
            if (!spawnCamera)
            {
                spawnCamera = Camera.main;
            }

            EnsureAudioSource();
        }

        private void OnEnable()
        {
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
            CountExistingEnemies();
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;
        }

        public void SpawnWave(int wave)
        {
            if (!player)
            {
                return;
            }

            EnsureAudioSource();
            bool isBossWave = bossWaveInterval > 0 && wave > 0 && wave % bossWaveInterval == 0;
            float radius = GetSpawnRadius();

            for (int i = 0; i < spawnDefinitions.Count; i++)
            {
                EnemySpawnDefinition definition = spawnDefinitions[i];
                if (definition == null || definition.Prefabs == null || definition.Prefabs.Length == 0)
                {
                    continue;
                }

                if (!DefinitionIsActive(definition, wave, isBossWave))
                {
                    continue;
                }

                bool definitionIsBoss = definition.IsBoss || definition.SpawnOnlyOnBossWaves;
                int remainingAllowed = GetRemainingSpawnAllowance(definitionIsBoss);
                if (remainingAllowed <= 0)
                {
                    continue;
                }

                int count = Mathf.Min(remainingAllowed, definition.EvaluateSpawnCount(wave));
                if (count <= 0)
                {
                    continue;
                }

                EnemyWaveModifiers modifiers = definition.GetWaveModifiers(wave, defaultScaling);
                int spawned = SpawnDefinition(definition, count, radius, modifiers, definitionIsBoss);
                if (spawned > 0)
                {
                    PlaySpawnCue(definition.SpawnCue);
                }
            }
        }

        private bool DefinitionIsActive(EnemySpawnDefinition definition, int wave, bool isBossWave)
        {
            int startWave = definition.StartWave;
            if (wave < startWave)
            {
                return false;
            }

            if (definition.EndWave > 0 && wave > definition.EndWave)
            {
                return false;
            }

            if (definition.SpawnOnlyOnBossWaves)
            {
                if (!isBossWave)
                {
                    return false;
                }
            }
            else if (isBossWave && !spawnNonBossDefinitionsDuringBossWave)
            {
                return false;
            }

            int interval = Mathf.Max(1, definition.SpawnInterval);
            int relativeWave = wave - startWave;
            if (relativeWave < 0 || relativeWave % interval != 0)
            {
                return false;
            }

            float chance = definition.SpawnChance;
            if (chance < 1f && Random.value > Mathf.Clamp01(chance))
            {
                return false;
            }

            return true;
        }

        private int SpawnDefinition(EnemySpawnDefinition definition, int count, float radius, EnemyWaveModifiers modifiers, bool isBoss)
        {
            if (count <= 0)
            {
                return 0;
            }

            int spawned = 0;
            if (definition.SpawnInPacks)
            {
                Vector2 baseDirection = Random.insideUnitCircle.normalized;
                if (baseDirection.sqrMagnitude < Mathf.Epsilon)
                {
                    baseDirection = Vector2.right;
                }

                Vector2 anchor = FindSpawnPosition(baseDirection, radius);
                for (int i = 0; i < count; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * definition.PackRadius;
                    Vector2 spawnPosition = anchor + offset;
                    if (SpawnEnemy(ChooseRandomPrefab(definition), spawnPosition, modifiers, isBoss))
                    {
                        spawned++;
                    }
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 spawnPosition = FindSpawnPosition(direction, radius);
                    if (SpawnEnemy(ChooseRandomPrefab(definition), spawnPosition, modifiers, isBoss))
                    {
                        spawned++;
                    }
                }
            }

            return spawned;
        }

        private GameObject ChooseRandomPrefab(EnemySpawnDefinition def)
        {
            if (def.Prefabs == null || def.Prefabs.Length == 0)
                return null;

            int index = Random.Range(0, def.Prefabs.Length);
            return def.Prefabs[index];
        }

        private bool SpawnEnemy(GameObject prefab, Vector2 position, EnemyWaveModifiers modifiers, bool isBoss)
        {
            var enemyInstance = Instantiate(prefab, position, Quaternion.identity);
            if (!enemyInstance)
            {
                return false;
            }

            if (enemyInstance.TryGetComponent(out Enemy enemy))
            {
                enemy.SetIsBoss(isBoss);
                enemy.Initialize(player);
                enemy.ApplyWaveModifiers(modifiers);
                IncrementSpawnCount(isBoss);
            }

            return true;
        }

        private float GetSpawnRadius()
        {
            float radius = spawnRadius;
            float cameraRadius = GetCameraViewRadius();
            if (cameraRadius > 0f)
            {
                radius = Mathf.Max(radius, cameraRadius + spawnBuffer);
            }

            return radius;
        }

        private Vector2 FindSpawnPosition(Vector2 direction, float baseRadius)
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

        private bool IsPointVisible(Vector2 worldPosition)
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

        private float GetCameraViewRadius()
        {
            if (!spawnCamera || !spawnCamera.orthographic)
            {
                return 0f;
            }

            float halfHeight = spawnCamera.orthographicSize;
            float halfWidth = halfHeight * spawnCamera.aspect;
            return Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
        }

        private void OnValidate()
        {
            spawnRadius = Mathf.Max(1f, spawnRadius);
            spawnBuffer = Mathf.Max(0f, spawnBuffer);
            maxSpawnAttempts = Mathf.Max(1, maxSpawnAttempts);
            bossWaveInterval = Mathf.Max(1, bossWaveInterval);
            maxActiveBosses = Mathf.Max(0, maxActiveBosses);
            maxActiveNonBosses = Mathf.Max(0, maxActiveNonBosses);
            maxActiveTotal = Mathf.Max(0, maxActiveTotal);
            EnsureAudioSource();
        }

        #region Audio
        private void EnsureAudioSource()
        {
            if (audioSource)
            {
                return;
            }

            audioSource = GetComponent<AudioSource>();
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        private void PlaySpawnCue(AudioClip clip)
        {
            AudioClip finalClip = clip ? clip : fallbackSpawnClip;
            if (!finalClip || !audioSource)
            {
                return;
            }

            audioSource.PlayOneShot(finalClip);
        }
        #endregion Audio

        #region Tracking
        private void IncrementSpawnCount(bool isBoss)
        {
            if (isBoss)
            {
                _activeBosses++;
            }
            else
            {
                _activeNonBosses++;
            }
        }

        private void DecrementSpawnCount(bool isBoss)
        {
            if (isBoss)
            {
                _activeBosses = Mathf.Max(0, _activeBosses - 1);
            }
            else
            {
                _activeNonBosses = Mathf.Max(0, _activeNonBosses - 1);
            }
        }

        private void HandleEnemyKilled(Enemy enemy)
        {
            if (!enemy)
            {
                return;
            }

            DecrementSpawnCount(enemy.IsBoss);
        }

        private void CountExistingEnemies()
        {
            _activeBosses = 0;
            _activeNonBosses = 0;

            Enemy[] existing = FindObjectsOfType<Enemy>();
            for (int i = 0; i < existing.Length; i++)
            {
                Enemy enemy = existing[i];
                if (!enemy)
                {
                    continue;
                }

                IncrementSpawnCount(enemy.IsBoss);
            }
        }

        private int GetRemainingSpawnAllowance(bool isBoss)
        {
            int totalLimit = maxActiveTotal > 0 ? maxActiveTotal : int.MaxValue;
            int bossLimit = maxActiveBosses > 0 ? maxActiveBosses : int.MaxValue;
            int nonBossLimit = maxActiveNonBosses > 0 ? maxActiveNonBosses : int.MaxValue;

            int totalRemaining = Mathf.Max(0, totalLimit - (_activeBosses + _activeNonBosses));
            int typeRemaining = isBoss
                ? Mathf.Max(0, bossLimit - _activeBosses)
                : Mathf.Max(0, nonBossLimit - _activeNonBosses);

            return Mathf.Min(totalRemaining, typeRemaining);
        }
        #endregion Tracking
    }
}
