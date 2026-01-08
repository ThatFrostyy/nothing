using Steamworks;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;


namespace FF
{
    public class GameManager : NetworkBehaviour, ISceneReferenceHandler
    {
        public float CurrentWaveInterval => currentWaveInterval;

        [SerializeField] private EnemySpawner spawner;
        [FormerlySerializedAs("timeBetweenWaves")]
        [SerializeField, Min(0f)] private float initialTimeBetweenWaves = 30f;
        [SerializeField, Min(0f)] private float waveIntervalIncrease = 5f;
        [SerializeField, Min(0f)] private float maximumTimeBetweenWaves = 60f;
        [Header("FX")]
        [SerializeField] private GameObject enemyDeathFx;//

        float timer;
        float currentWaveInterval;
        bool spawningEnabled = true;

        public readonly NetworkVariable<int> Wave = new();
        public readonly NetworkVariable<int> KillCount = new();
        public readonly NetworkVariable<int> BossKillCount = new();
        public readonly NetworkVariable<int> CratesDestroyedCount = new();
        public readonly NetworkVariable<float> RunTimeSeconds = new();

        public event Action<int> OnKillCountChanged;
        public event Action<int> OnWaveStarted;
        public event Action<int> OnBossKillCountChanged;
        public event Action<int> OnCratesDestroyedCountChanged;
        public event Action<float> OnRunTimeSecondsChanged;

        public event Action<EnemySpawner> OnSpawnerRegistered;

        private bool IsAuthority => IsServer || (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);

        private static GameManager _instance;
        public static GameManager I
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<GameManager>();
                return _instance;
            }
        }

        void Awake()
        {
            Application.targetFrameRate = -1;
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            ResetGameState();
            EnsureDebugConsole();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            KillCount.OnValueChanged += (prev, current) => OnKillCountChanged?.Invoke(current);
            Wave.OnValueChanged += (prev, current) => OnWaveStarted?.Invoke(current);
            BossKillCount.OnValueChanged += (prev, current) => OnBossKillCountChanged?.Invoke(current);
            CratesDestroyedCount.OnValueChanged += (prev, current) => OnCratesDestroyedCountChanged?.Invoke(current);
            RunTimeSeconds.OnValueChanged += (prev, current) => OnRunTimeSecondsChanged?.Invoke(current);
        }

        void OnEnable()
        {
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
            WeaponCrate.OnAnyBroken += HandleCrateBroken;

            SceneReferenceRegistry.Register(this);
        }

        void OnDisable()
        {
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;
            WeaponCrate.OnAnyBroken -= HandleCrateBroken;

            SceneReferenceRegistry.Unregister(this);
        }

        public void RegisterSpawner(EnemySpawner spawner)
        {
            this.spawner = spawner;
            OnSpawnerRegistered?.Invoke(spawner);
        }

        public void ClearSceneReferences()
        {
            spawner = null;
        }

        public void ResetGameState()
        {
            if (!IsAuthority) return;

            KillCount.Value = 0;
            Wave.Value = 0;
            timer = 0f;
            BossKillCount.Value = 0;
            CratesDestroyedCount.Value = 0;
            RunTimeSeconds.Value = 0f;

            spawningEnabled = true;

            float cap = maximumTimeBetweenWaves <= 0f ? float.MaxValue : maximumTimeBetweenWaves;
            currentWaveInterval = Mathf.Clamp(initialTimeBetweenWaves, 0f, cap);

            ClearSceneReferences();
        }

        void Update()
        {
            if (!IsAuthority) return;

            if (spawner != null && spawningEnabled)
            {
                RunTimeSeconds.Value += Time.deltaTime;
            }

            if (spawner == null || !spawningEnabled)
                return;

            timer += Time.deltaTime;
            float interval = Mathf.Max(0.01f, currentWaveInterval);

            if (timer >= interval)
            {
                timer = 0f;
                Wave.Value++;

                if (spawner)
                {
                    spawner.SpawnWave(Wave.Value);
                }

                AdvanceWaveInterval();
            }
        }

        void HandleEnemyKilled(Enemy enemy)
        {
            if (!IsAuthority) return;
            KillCount.Value++;

            if (enemy != null && enemy.IsBoss)
            {
                BossKillCount.Value++;
            }

            SpawnEnemyDeathFx(enemy);
        }

        private void HandleCrateBroken(WeaponCrate crate)
        {
            if (!IsAuthority) return;
            _ = crate;
            CratesDestroyedCount.Value++;
        }

        private void SpawnEnemyDeathFx(Enemy enemy)
        {
            if (!enemyDeathFx || !enemy)
            {
                return;
            }

            GameObject spawned = PoolManager.Get(enemyDeathFx, enemy.transform.position, Quaternion.identity);
            if (spawned && !spawned.TryGetComponent<PooledParticleSystem>(out var pooled))
            {
                pooled = spawned.AddComponent<PooledParticleSystem>();
                pooled.OnTakenFromPool();
            }
        }

        void AdvanceWaveInterval()
        {
            if (waveIntervalIncrease <= 0f)
            {
                return;
            }

            float cap = maximumTimeBetweenWaves <= 0f ? float.MaxValue : maximumTimeBetweenWaves;
            currentWaveInterval = Mathf.Min(cap, currentWaveInterval + waveIntervalIncrease);
        }

        void OnValidate()
        {
            initialTimeBetweenWaves = Mathf.Max(0f, initialTimeBetweenWaves);
            waveIntervalIncrease = Mathf.Max(0f, waveIntervalIncrease);
            maximumTimeBetweenWaves = Mathf.Max(0f, maximumTimeBetweenWaves);
            float cap = maximumTimeBetweenWaves <= 0f ? float.MaxValue : maximumTimeBetweenWaves;
            currentWaveInterval = Mathf.Clamp(initialTimeBetweenWaves, 0f, cap);
        }

        public void SetSpawningEnabled(bool enabled, bool stopActiveSpawns = false)
        {
            if (!IsAuthority) return;
            spawningEnabled = enabled;

            if (stopActiveSpawns)
            {
                timer = 0f;
                spawner?.StopSpawning();
            }
        }

        public bool DebugStartNextWave()
        {
            if (!IsAuthority || spawner == null)
            {
                return false;
            }

            timer = 0f;
            Wave.Value++;

            spawner.SpawnWave(Wave.Value);
            AdvanceWaveInterval();

            return true;
        }

        public bool DebugStartWave(int wave)
        {
            if (!IsAuthority || spawner == null)
            {
                return false;
            }

            int targetWave = Mathf.Max(1, wave);
            timer = 0f;
            Wave.Value = targetWave;

            spawner.SpawnWave(Wave.Value);
            AdvanceWaveInterval();

            return true;
        }

        private void EnsureDebugConsole()
        {
            if (!DebugConsole.IsDebugEnabled)
            {
                return;
            }

            if (FindFirstObjectByType<DebugConsole>() != null)
            {
                return;
            }

            GameObject consoleObject = new GameObject("DebugConsole");
            consoleObject.AddComponent<DebugConsole>();
            DontDestroyOnLoad(consoleObject);
        }
    }
}
