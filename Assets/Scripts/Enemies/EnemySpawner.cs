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

        [Header("Culling")]
        [SerializeField, Min(0f)] private float cullDistance = 0f;
        [SerializeField, Min(0f)] private float cullInterval = 1f;
        [SerializeField, Min(0)] private int maxCulledPerPass = 6;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip fallbackSpawnClip;

        private int _activeBosses;
        private int _activeNonBosses;
        private Coroutine _spawnRoutine;
        private float _nextCullTime;

        private readonly HashSet<Enemy> _activeEnemies = new();
        private readonly List<Enemy> _cullBuffer = new();

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

            _activeEnemies.Clear();
        }

        public void SpawnWave(int wave)
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

            _spawnRoutine = StartCoroutine(SpawnWaveRoutine(wave));
        }

        public void StopSpawning()
        {
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
        }

        private void Update()
        {
            if (cullDistance <= 0f || !player)
            {
                return;
            }

            if (Time.unscaledTime < _nextCullTime)
            {
                return;
            }

            _nextCullTime = Time.unscaledTime + Mathf.Max(0.1f, cullInterval);
            CullFarEnemies();
        }

        private System.Collections.IEnumerator SpawnWaveRoutine(int wave)
        {
            bool isBossWave = bossWaveInterval > 0 && wave > 0 && wave % bossWaveInterval == 0;
            float radius = GetSpawnRadius();

            List<SpawnRequest> requests = BuildSpawnRequests(wave, isBossWave);
            if (requests.Count == 0)
            {
                _spawnRoutine = null;
                yield break;
            }

            List<Vector2> primarySides = Random.value > 0.5f
                ? new List<Vector2> { Vector2.left, Vector2.right }
                : new List<Vector2> { Vector2.up, Vector2.down };
            List<Vector2> allSides = new() { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

            float elapsed = 0f;
            int requestIndex = 0;
            int idleIterations = 0;

            while (HasPendingRequests(requests))
            {
                float interval = GetCurrentSpawnInterval(elapsed);
                Vector2 direction = SelectDirection(elapsed < sideSwapDelay ? primarySides : allSides);
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
                elapsed += interval;
                yield return new WaitForSeconds(interval);
            }

            _spawnRoutine = null;
        }

        private List<SpawnRequest> BuildSpawnRequests(int wave, bool isBossWave)
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

            for (int i = 0; i < spawnDefinitions.Count; i++)
            {
                EnemySpawnDefinition definition = spawnDefinitions[i];
                if (definition == null || definition.Prefabs == null || definition.Prefabs.Length == 0)
                    continue;

                if (!definition.HasPrefabOverrides)
                {
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

                    requests.Add(new SpawnRequest(definition, count, modifiers, definitionIsBoss, null));
                    continue;
                }

                List<EnemySpawnDefinition.PrefabEntry> activePrefabs = definition.GetEligiblePrefabs(wave, isBossWave, spawnNonBossDefinitionsDuringBossWave);
                if (activePrefabs.Count == 0)
                {
                    continue;
                }

                int baseCount = definition.EvaluateSpawnCount(wave);
                int baseBossCount = (definition.IsBoss || definition.SpawnOnlyOnBossWaves) ? baseCount : 0;
                int baseNonBossCount = (!definition.IsBoss && !definition.SpawnOnlyOnBossWaves) ? baseCount : 0;

                var defaultBossPrefabs = new List<GameObject>();
                var defaultNonBossPrefabs = new List<GameObject>();
                var customBossEntries = new List<EnemySpawnDefinition.PrefabEntry>();
                var customNonBossEntries = new List<EnemySpawnDefinition.PrefabEntry>();

                for (int p = 0; p < activePrefabs.Count; p++)
                {
                    EnemySpawnDefinition.PrefabEntry entry = activePrefabs[p];

                    if (entry.HasSpawnCountOverride)
                    {
                        int remainingAllowed = VirtualRemaining(entry.IsBoss);
                        if (remainingAllowed <= 0)
                        {
                            continue;
                        }

                        int desired = entry.EvaluateSpawnCount(wave, 0);
                        int count = Mathf.Min(desired, remainingAllowed);
                        if (count <= 0)
                        {
                            continue;
                        }

                        if (entry.IsBoss)
                        {
                            baseBossCount = Mathf.Max(0, baseBossCount - count);
                            virtualActiveBosses += count;
                        }
                        else
                        {
                            baseNonBossCount = Mathf.Max(0, baseNonBossCount - count);
                            virtualActiveNonBosses += count;
                        }

                        EnemyWaveModifiers modifiers = entry.BuildWaveModifiers(wave, defaultScaling);
                        requests.Add(new SpawnRequest(definition, count, modifiers, entry.IsBoss, new List<GameObject> { entry.Prefab }));
                        continue;
                    }

                    if (entry.IsBoss)
                    {
                        if (entry.HasScalingOverride)
                        {
                            customBossEntries.Add(entry);
                        }
                        else
                        {
                            defaultBossPrefabs.Add(entry.Prefab);
                        }
                    }
                    else
                    {
                        if (entry.HasScalingOverride)
                        {
                            customNonBossEntries.Add(entry);
                        }
                        else
                        {
                            defaultNonBossPrefabs.Add(entry.Prefab);
                        }
                    }
                }

                int bossBuckets = customBossEntries.Count + (defaultBossPrefabs.Count > 0 ? 1 : 0);
                int bossShare = bossBuckets > 0 ? Mathf.CeilToInt((float)baseBossCount / bossBuckets) : 0;

                for (int b = 0; b < customBossEntries.Count; b++)
                {
                    EnemySpawnDefinition.PrefabEntry entry = customBossEntries[b];
                    int remainingAllowed = VirtualRemaining(true);
                    int desired = Mathf.Min(bossShare, remainingAllowed, baseBossCount);
                    if (desired > 0)
                    {
                        EnemyWaveModifiers modifiers = entry.BuildWaveModifiers(wave, defaultScaling);
                        requests.Add(new SpawnRequest(definition, desired, modifiers, true, new List<GameObject> { entry.Prefab }));
                        baseBossCount = Mathf.Max(0, baseBossCount - desired);
                        virtualActiveBosses += desired;
                    }
                }

                int nonBossBuckets = customNonBossEntries.Count + (defaultNonBossPrefabs.Count > 0 ? 1 : 0);
                int nonBossShare = nonBossBuckets > 0 ? Mathf.CeilToInt((float)baseNonBossCount / nonBossBuckets) : 0;

                for (int n = 0; n < customNonBossEntries.Count; n++)
                {
                    EnemySpawnDefinition.PrefabEntry entry = customNonBossEntries[n];
                    int remainingAllowed = VirtualRemaining(false);
                    int desired = Mathf.Min(nonBossShare, remainingAllowed, baseNonBossCount);
                    if (desired > 0)
                    {
                        EnemyWaveModifiers modifiers = entry.BuildWaveModifiers(wave, defaultScaling);
                        requests.Add(new SpawnRequest(definition, desired, modifiers, false, new List<GameObject> { entry.Prefab }));
                        baseNonBossCount = Mathf.Max(0, baseNonBossCount - desired);
                        virtualActiveNonBosses += desired;
                    }
                }

                if (defaultBossPrefabs.Count > 0)
                {
                    int remainingAllowed = VirtualRemaining(true);
                    int count = Mathf.Min(baseBossCount, remainingAllowed);
                    if (count > 0)
                    {
                        EnemyWaveModifiers modifiers = definition.GetWaveModifiers(wave, defaultScaling);
                        requests.Add(new SpawnRequest(definition, count, modifiers, true, defaultBossPrefabs));
                        virtualActiveBosses += count;
                    }
                }

                if (defaultNonBossPrefabs.Count > 0)
                {
                    int remainingAllowed = VirtualRemaining(false);
                    int count = Mathf.Min(baseNonBossCount, remainingAllowed);
                    if (count > 0)
                    {
                        EnemyWaveModifiers modifiers = definition.GetWaveModifiers(wave, defaultScaling);
                        requests.Add(new SpawnRequest(definition, count, modifiers, false, defaultNonBossPrefabs));
                        virtualActiveNonBosses += count;
                    }
                }
            }

            return requests;
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

            int spawned = SpawnBatch(request, batchSize, direction, radius);
            request.Remaining = Mathf.Max(0, request.Remaining - spawned);
            return spawned > 0;
        }

        private int SpawnBatch(SpawnRequest request, int count, Vector2 direction, float radius)
        {
            int spawned = 0;
            EnemySpawnDefinition definition = request.Definition;
            Vector2 baseDirection = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Random.insideUnitCircle.normalized;
            if (definition.SpawnInPacks)
            {
                Vector2 anchor = FindSpawnPosition(baseDirection, radius);
                for (int i = 0; i < count; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * definition.PackRadius;
                    Vector2 spawnPosition = anchor + offset;
                    if (SpawnEnemy(ChooseRandomPrefab(definition, request.Prefabs), spawnPosition, request.Modifiers, request.IsBoss))
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
                    if (SpawnEnemy(ChooseRandomPrefab(definition, request.Prefabs), spawnPosition, request.Modifiers, request.IsBoss))
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
            public System.Collections.Generic.IReadOnlyList<GameObject> Prefabs { get; }

            public SpawnRequest(EnemySpawnDefinition definition, int count, EnemyWaveModifiers modifiers, bool isBoss, System.Collections.Generic.IReadOnlyList<GameObject> prefabs)
            {
                Definition = definition;
                Remaining = count;
                Modifiers = modifiers;
                IsBoss = isBoss;
                Prefabs = prefabs;
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

        private GameObject ChooseRandomPrefab(EnemySpawnDefinition def, System.Collections.Generic.IReadOnlyList<GameObject> candidates)
        {
            if (candidates != null && candidates.Count > 0)
            {
                int candidateIndex = Random.Range(0, candidates.Count);
                return candidates[candidateIndex];
            }

            if (def.Prefabs == null || def.Prefabs.Length == 0)
                return null;

            int prefabIndex = Random.Range(0, def.Prefabs.Length);
            return def.Prefabs[prefabIndex];
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
            _activeEnemies.Add(enemy);

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
            cullDistance = Mathf.Max(0f, cullDistance);
            cullInterval = Mathf.Max(0f, cullInterval);
            maxCulledPerPass = Mathf.Max(0, maxCulledPerPass);
            initialSpawnInterval = Mathf.Max(0.01f, initialSpawnInterval);
            minimumSpawnInterval = Mathf.Clamp(Mathf.Max(0.01f, minimumSpawnInterval), 0.01f, initialSpawnInterval);
            spawnRampDuration = Mathf.Max(0f, spawnRampDuration);
            sideSwapDelay = Mathf.Max(0f, sideSwapDelay);
            packSpawnBurst = Mathf.Max(1, packSpawnBurst);
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
            _activeEnemies.Remove(enemy);
        }

        private void CountExistingEnemies()
        {
            _activeBosses = 0;
            _activeNonBosses = 0;
            _activeEnemies.Clear();
            Enemy[] existing = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                Enemy enemy = existing[i];
                if (!enemy)
                {
                    continue;
                }

                IncrementSpawnCount(enemy.IsBoss);
                _activeEnemies.Add(enemy);
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

        private void CullFarEnemies()
        {
            if (maxCulledPerPass == 0)
            {
                return;
            }

            float maxDistanceSqr = cullDistance * cullDistance;
            if (maxDistanceSqr <= 0f)
            {
                return;
            }

            _cullBuffer.Clear();
            foreach (var enemy in _activeEnemies)
            {
                if (!enemy)
                {
                    _cullBuffer.Add(enemy);
                    continue;
                }

                Vector2 toEnemy = enemy.transform.position - player.position;
                if (toEnemy.sqrMagnitude > maxDistanceSqr)
                {
                    _cullBuffer.Add(enemy);
                }
            }

            if (_cullBuffer.Count == 0)
            {
                return;
            }

            int culled = 0;
            for (int i = 0; i < _cullBuffer.Count && culled < maxCulledPerPass; i++)
            {
                Enemy enemy = _cullBuffer[i];
                if (!enemy)
                {
                    _activeEnemies.Remove(enemy);
                    continue;
                }

                ForceDespawn(enemy);
                culled++;
            }
        }

        private void ForceDespawn(Enemy enemy)
        {
            if (!enemy)
            {
                return;
            }

            bool isBoss = enemy.IsBoss;
            if (enemy.TryGetComponent(out PoolToken token) && token.Owner != null)
            {
                token.Release();
            }
            else
            {
                Destroy(enemy.gameObject);
            }

            DecrementSpawnCount(isBoss);
            _activeEnemies.Remove(enemy);
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
