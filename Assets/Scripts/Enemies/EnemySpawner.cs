using System;
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
        [SerializeField] private List<WaveSpike> waveSpikes = new();

        [Header("Spawn Timing")]
        [SerializeField, Min(0.01f)] private float initialSpawnInterval = 0.6f;
        [SerializeField, Min(0.01f)] private float minimumSpawnInterval = 0.18f;
        [SerializeField, Min(0f)] private float spawnRampDuration = 1.25f;
        [SerializeField, Min(0f)] private float sideSwapDelay = 1.15f;
        [SerializeField, Min(1)] private int packSpawnBurst = 3;

        [Header("Pooling")]
        [SerializeField] private Transform poolParent;
        [SerializeField] private bool prewarmPoolsOnAwake = true;

        [Header("Limits (0 = Unlimited)")]
        [SerializeField, Min(0)] private int maxActiveNonBosses = 0;
        [SerializeField, Min(0)] private int maxActiveBosses = 0;
        [SerializeField, Min(0)] private int maxActiveTotal = 0;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip fallbackSpawnClip;

        private int _activeBosses;
        private int _activeNonBosses;
        private Coroutine _spawnRoutine;

        private void Awake()
        {
            if (!spawnCamera)
            {
                spawnCamera = Camera.main;
            }

            EnsureAudioSource();

            EnsurePoolParent();

            if (prewarmPoolsOnAwake)
            {
                PrewarmEnemyPools();
            }
        }

        private void Start()
        {
            if (GameManager.I != null)
            {
                GameManager.I.RegisterSpawner(this);
            }
        }

        private void OnEnable()
        {
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
            CountExistingEnemies();
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;

            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
        }

        public void SpawnWave(int wave, float waveDuration)
        {

            if (!player)
            {
                return;
            }

            EnsureAudioSource();
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
            }

            _spawnRoutine = StartCoroutine(SpawnWaveRoutine(wave, waveDuration));
        }

        public void EndWave()
        {
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
        }

        private System.Collections.IEnumerator SpawnWaveRoutine(int wave, float waveDuration)
        {
            bool isBossWave = bossWaveInterval > 0 && wave > 0 && wave % bossWaveInterval == 0;
            float radius = GetSpawnRadius();

            List<SpawnRequest> baseRequests = BuildSpawnRequests(wave, isBossWave);
            WaveSpike spike = FindSpikeForWave(wave);
            List<SpawnRequest> spikeRequests = spike != null
                ? BuildSpawnRequests(wave, isBossWave, spike.SpawnDefinitions)
                : null;

            List<Vector2> primarySides = Random.value > 0.5f
                ? new List<Vector2> { Vector2.left, Vector2.right }
                : new List<Vector2> { Vector2.up, Vector2.down };
            List<Vector2> allSides = new() { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

            float elapsed = 0f;
            int requestIndex = 0;
            int idleIterations = 0;

            while (elapsed < waveDuration)
            {
                bool spikeActive = spikeRequests != null && elapsed < spike.SpikeDuration;
                List<SpawnRequest> requests = spikeActive
                    ? MergeRequests(baseRequests, spikeRequests)
                    : baseRequests;

                if (!HasPendingRequests(requests))
                {
                    baseRequests = BuildSpawnRequests(wave, isBossWave);
                    if (spikeRequests != null && spikeActive)
                    {
                        spikeRequests = BuildSpawnRequests(wave, isBossWave, spike.SpawnDefinitions);
                    }

                    requestIndex = 0;
                    idleIterations = 0;
                    requests = spikeActive ? MergeRequests(baseRequests, spikeRequests) : baseRequests;

                    if (!HasPendingRequests(requests))
                    {
                        float idleWait = Mathf.Min(0.25f, waveDuration - elapsed);
                        if (idleWait > 0f)
                        {
                            elapsed += idleWait;
                            yield return new WaitForSeconds(idleWait);
                        }
                        continue;
                    }
                }

                if (requests.Count == 0)
                {
                    break;
                }

                float interval = GetCurrentSpawnInterval(elapsed);
                Vector2 direction = SelectDirection(elapsed < sideSwapDelay ? primarySides : allSides);
                requestIndex = requestIndex % requests.Count;
                SpawnRequest current = requests[requestIndex];

                bool spawned = TrySpawnFromRequest(current, direction, radius);
                if (spawned && !current.CuePlayed)
                {
                    PlaySpawnCue(current.Definition.SpawnCue);
                    current.CuePlayed = true;
                }

                if (!spawned)
                {
                    idleIterations++;
                    if (idleIterations >= requests.Count)
                    {
                        break;
                    }
                }
                else
                {
                    idleIterations = 0;
                }

                requestIndex = (requestIndex + 1) % requests.Count;
                float waitTime = Mathf.Min(interval, waveDuration - elapsed);
                if (waitTime > 0f)
                {
                    yield return new WaitForSeconds(waitTime);
                }

                elapsed += interval;
            }

            _spawnRoutine = null;
        }

        private List<SpawnRequest> BuildSpawnRequests(int wave, bool isBossWave, IList<EnemySpawnDefinition> definitionsOverride = null)
        {
            var requests = new List<SpawnRequest>();

            int virtualActiveBosses = _activeBosses;
            int virtualActiveNonBosses = _activeNonBosses;

            int VirtualRemaining(bool isBoss)
            {
                int totalLimit = maxActiveTotal > 0 ? maxActiveTotal : int.MaxValue;
                int bossLimit = maxActiveBosses > 0 ? maxActiveBosses : int.MaxValue;
                int nonBossLimit = maxActiveNonBosses > 0 ? maxActiveNonBosses : int.MaxValue;

                int totalRemaining = Mathf.Max(0, totalLimit - (virtualActiveBosses + virtualActiveNonBosses));
                int typeRemaining = isBoss
                    ? Mathf.Max(0, bossLimit - virtualActiveBosses)
                    : Mathf.Max(0, nonBossLimit - virtualActiveNonBosses);

                return Mathf.Min(totalRemaining, typeRemaining);
            }

            IList<EnemySpawnDefinition> definitions = definitionsOverride ?? spawnDefinitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                EnemySpawnDefinition definition = definitions[i];
                if (definition == null || definition.Prefabs == null || definition.Prefabs.Length == 0)
                    continue;

                if (!DefinitionIsActive(definition, wave, isBossWave))
                    continue;

                bool definitionIsBoss = definition.IsBoss || definition.SpawnOnlyOnBossWaves;

                int remainingAllowed = VirtualRemaining(definitionIsBoss);
                if (remainingAllowed <= 0)
                    continue;

                int desired = definition.EvaluateSpawnCount(wave);
                int count = Mathf.Min(desired, remainingAllowed);

                if (count <= 0)
                    continue;

                if (definitionIsBoss)
                    virtualActiveBosses += count;
                else
                    virtualActiveNonBosses += count;

                EnemyWaveModifiers modifiers = definition.GetWaveModifiers(wave, defaultScaling);

                requests.Add(new SpawnRequest(definition, count, modifiers, definitionIsBoss));
            }

            return requests;
        }

        private static List<SpawnRequest> MergeRequests(List<SpawnRequest> primary, List<SpawnRequest> secondary)
        {
            if (secondary == null || secondary.Count == 0)
            {
                return primary ?? new List<SpawnRequest>();
            }

            var merged = new List<SpawnRequest>();
            if (primary != null)
            {
                merged.AddRange(primary);
            }

            merged.AddRange(secondary);
            return merged;
        }

        private bool HasPendingRequests(List<SpawnRequest> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < requests.Count; i++)
            {
                if (requests[i] != null && requests[i].Remaining > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TrySpawnFromRequest(SpawnRequest request, Vector2 direction, float radius)
        {
            if (request == null || request.Definition == null || request.Remaining <= 0)
            {
                return false;
            }

            int allowance = GetRemainingSpawnAllowance(request.IsBoss);
            if (allowance <= 0)
            {
                return false;
            }

            int batchSize = request.Definition.SpawnInPacks
                ? Mathf.Min(request.Remaining, Mathf.Max(1, packSpawnBurst))
                : 1;

            batchSize = Mathf.Min(batchSize, allowance);
            if (batchSize <= 0)
            {
                return false;
            }

            int spawned = SpawnBatch(request.Definition, batchSize, direction, radius, request.Modifiers, request.IsBoss);
            request.Remaining = Mathf.Max(0, request.Remaining - spawned);
            return spawned > 0;
        }

        private int SpawnBatch(EnemySpawnDefinition definition, int count, Vector2 direction, float radius, EnemyWaveModifiers modifiers, bool isBoss)
        {
            int spawned = 0;
            Vector2 baseDirection = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Random.insideUnitCircle.normalized;
            if (definition.SpawnInPacks)
            {
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
                    Vector2 spawnDirection = baseDirection;
                    if (spawnDirection.sqrMagnitude < Mathf.Epsilon)
                    {
                        float angle = Random.value * Mathf.PI * 2f;
                        spawnDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    }

                    Vector2 spawnPosition = FindSpawnPosition(spawnDirection, radius);
                    if (SpawnEnemy(ChooseRandomPrefab(definition), spawnPosition, modifiers, isBoss))
                    {
                        spawned++;
                    }
                }
            }

            return spawned;
        }

        private Vector2 SelectDirection(List<Vector2> sides)
        {
            if (sides == null || sides.Count == 0)
            {
                Vector2 fallback = Random.insideUnitCircle;
                return fallback.sqrMagnitude > Mathf.Epsilon ? fallback.normalized : Vector2.right;
            }

            Vector2 choice = sides[Random.Range(0, sides.Count)];
            return choice.sqrMagnitude > Mathf.Epsilon ? choice.normalized : Vector2.right;
        }

        private float GetCurrentSpawnInterval(float elapsed)
        {
            float t = spawnRampDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / spawnRampDuration);
            float interval = Mathf.Lerp(initialSpawnInterval, minimumSpawnInterval, t);
            return Mathf.Max(0.01f, interval);
        }

        private sealed class SpawnRequest
        {
            public EnemySpawnDefinition Definition { get; }
            public int Remaining { get; set; }
            public EnemyWaveModifiers Modifiers { get; }
            public bool IsBoss { get; }
            public bool CuePlayed { get; set; }

            public SpawnRequest(EnemySpawnDefinition definition, int count, EnemyWaveModifiers modifiers, bool isBoss)
            {
                Definition = definition;
                Remaining = count;
                Modifiers = modifiers;
                IsBoss = isBoss;
            }
        }

        [Serializable]
        private class WaveSpike
        {
            [SerializeField, Min(1)] private int wave = 1;
            [SerializeField, Min(0f)] private float spikeDuration = 5f;
            [SerializeField] private List<EnemySpawnDefinition> spawnDefinitions = new();

            public int Wave => Mathf.Max(1, wave);
            public float SpikeDuration => Mathf.Max(0f, spikeDuration);
            public List<EnemySpawnDefinition> SpawnDefinitions => spawnDefinitions;

            public void OnValidate()
            {
                wave = Mathf.Max(1, wave);
                spikeDuration = Mathf.Max(0f, spikeDuration);
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

        private WaveSpike FindSpikeForWave(int wave)
        {
            if (waveSpikes == null)
            {
                return null;
            }

            for (int i = 0; i < waveSpikes.Count; i++)
            {
                WaveSpike spike = waveSpikes[i];
                if (spike != null && spike.Wave == wave)
                {
                    return spike;
                }
            }

            return null;
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
            var enemyInstance = PoolManager.Get(prefab, position, Quaternion.identity);
            if (!enemyInstance)
            {
                return false;
            }

            if (!enemyInstance.TryGetComponent(out Enemy enemy))
            {
                PoolManager.Release(enemyInstance);
                return false;
            }

            enemy.SetIsBoss(isBoss);
            enemy.Initialize(player);
            enemy.ApplyWaveModifiers(modifiers);
            IncrementSpawnCount(isBoss);

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
            initialSpawnInterval = Mathf.Max(0.01f, initialSpawnInterval);
            minimumSpawnInterval = Mathf.Clamp(Mathf.Max(0.01f, minimumSpawnInterval), 0.01f, initialSpawnInterval);
            spawnRampDuration = Mathf.Max(0f, spawnRampDuration);
            sideSwapDelay = Mathf.Max(0f, sideSwapDelay);
            packSpawnBurst = Mathf.Max(1, packSpawnBurst);
            if (waveSpikes != null)
            {
                for (int i = 0; i < waveSpikes.Count; i++)
                {
                    waveSpikes[i]?.OnValidate();
                }
            }
            EnsureAudioSource();
            EnsurePoolParent();
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

            audioSource.PlayOneShot(finalClip, GameAudioSettings.SfxVolume);
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
            Enemy[] existing = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
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

        #region Pooling
        private void EnsurePoolParent()
        {
            if (poolParent)
            {
                return;
            }

            Transform existing = transform.Find("EnemyPool");
            if (existing)
            {
                poolParent = existing;
                return;
            }

            var container = new GameObject("EnemyPool");
            container.transform.SetParent(transform, false);
            poolParent = container.transform;
        }

        private void PrewarmEnemyPools()
        {
            if (spawnDefinitions == null)
            {
                return;
            }

            for (int i = 0; i < spawnDefinitions.Count; i++)
            {
                EnemySpawnDefinition definition = spawnDefinitions[i];
                if (definition == null || definition.Prefabs == null)
                {
                    continue;
                }

                int desiredWarmCount = Mathf.Max(1, definition.PoolPrewarmCount);
                if (definition.SpawnInPacks)
                {
                    desiredWarmCount = Mathf.Max(desiredWarmCount, packSpawnBurst);
                }

                for (int p = 0; p < definition.Prefabs.Length; p++)
                {
                    GameObject prefab = definition.Prefabs[p];
                    if (!prefab)
                    {
                        continue;
                    }

                    PoolManager.GetPool(prefab, desiredWarmCount, poolParent);
                }
            }
        }
        #endregion Pooling
    }
}
