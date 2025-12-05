using System;
using UnityEngine;
using UnityEngine.Serialization;


namespace FF
{
    public class GameManager : MonoBehaviour, ISceneReferenceHandler
    {
        public float CurrentWaveInterval => currentWaveInterval;

        [SerializeField] private EnemySpawner spawner;
        [FormerlySerializedAs("timeBetweenWaves")]
        [SerializeField, Min(0f)] private float initialTimeBetweenWaves = 30f;
        [SerializeField, Min(0f)] private float waveIntervalIncrease = 5f;
        [SerializeField, Min(0f)] private float maximumTimeBetweenWaves = 60f;
        [Header("FX")]
        [SerializeField] private GameObject enemyDeathFx;

        float timer;
        float currentWaveInterval;

        public int Wave { get; private set; } = 0;
        public int KillCount { get; private set; } = 0;

        public event Action<int> OnKillCountChanged;
        public event Action<int> OnWaveStarted;

        public event Action<EnemySpawner> OnSpawnerRegistered;

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
        }

        void OnEnable()
        {
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;

            SceneReferenceRegistry.Register(this);
        }

        void OnDisable()
        {
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;

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
            KillCount = 0;
            Wave = 0;
            timer = 0f;

            float cap = maximumTimeBetweenWaves <= 0f ? float.MaxValue : maximumTimeBetweenWaves;
            currentWaveInterval = Mathf.Clamp(initialTimeBetweenWaves, 0f, cap);

            ClearSceneReferences();

            OnKillCountChanged?.Invoke(KillCount);
        }

        void Update()
        {
            if (spawner == null)
                return;

            timer += Time.deltaTime;
            float interval = Mathf.Max(0.01f, currentWaveInterval);

            if (timer >= interval)
            {
                timer = 0f;
                Wave++;

                OnWaveStarted?.Invoke(Wave);

                if (spawner)
                {
                    spawner.SpawnWave(Wave);
                }

                AdvanceWaveInterval();
            }
        }

        void HandleEnemyKilled(Enemy enemy)
        {
            KillCount++;
            OnKillCountChanged?.Invoke(KillCount);

            SpawnEnemyDeathFx(enemy);
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
    }
}