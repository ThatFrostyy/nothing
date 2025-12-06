using System;
using UnityEngine;
using UnityEngine.Serialization;


namespace FF
{
    public class GameManager : MonoBehaviour, ISceneReferenceHandler
    {
        [SerializeField] private EnemySpawner spawner;
        [FormerlySerializedAs("timeBetweenWaves")]
        [SerializeField, Min(0f)] private float initialWaveDuration = 30f;
        [SerializeField, Min(0f)] private float waveDurationIncrease = 5f;
        [SerializeField, Min(0f)] private float maximumWaveDuration = 60f;
        [SerializeField, Min(0f)] private float initialTimeBetweenWaves = 10f;
        [SerializeField, Min(0f)] private float timeBetweenWavesIncrease = 0f;
        [SerializeField, Min(0f)] private float maximumTimeBetweenWaves = 20f;
        [Header("FX")]
        [SerializeField] private GameObject enemyDeathFx;

        private float timer;
        private float currentWaveDuration;
        private float currentTimeBetweenWaves;
        private WavePhase currentPhase = WavePhase.Idle;

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
            currentPhase = WavePhase.Idle;

            currentWaveDuration = ClampWaveDuration(initialWaveDuration);
            currentTimeBetweenWaves = ClampTimeBetweenWaves(initialTimeBetweenWaves);

            if (spawner)
            {
                spawner.EndWave();
            }

            ClearSceneReferences();

            OnKillCountChanged?.Invoke(KillCount);
        }

        void Update()
        {
            if (spawner == null)
                return;

            switch (currentPhase)
            {
                case WavePhase.Idle:
                    StartNextWave();
                    break;
                case WavePhase.Wave:
                    RunWaveTimer();
                    break;
                case WavePhase.Pause:
                    RunPauseTimer();
                    break;
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

        private void StartNextWave()
        {
            timer = 0f;
            Wave++;
            currentPhase = WavePhase.Wave;

            OnWaveStarted?.Invoke(Wave);

            if (spawner)
            {
                spawner.SpawnWave(Wave, currentWaveDuration);
            }
        }

        private void RunWaveTimer()
        {
            timer += Time.deltaTime;
            if (timer < Mathf.Max(0.01f, currentWaveDuration))
            {
                return;
            }

            timer = 0f;
            currentPhase = WavePhase.Pause;
            currentWaveDuration = ClampWaveDuration(currentWaveDuration + waveDurationIncrease);
            currentTimeBetweenWaves = ClampTimeBetweenWaves(currentTimeBetweenWaves + timeBetweenWavesIncrease);

            if (spawner)
            {
                spawner.EndWave();
            }
        }

        private void RunPauseTimer()
        {
            timer += Time.deltaTime;
            if (timer < Mathf.Max(0f, currentTimeBetweenWaves))
            {
                return;
            }

            StartNextWave();
        }

        private float ClampWaveDuration(float value)
        {
            float cap = maximumWaveDuration <= 0f ? float.MaxValue : maximumWaveDuration;
            return Mathf.Clamp(value, 0f, cap);
        }

        private float ClampTimeBetweenWaves(float value)
        {
            float cap = maximumTimeBetweenWaves <= 0f ? float.MaxValue : maximumTimeBetweenWaves;
            return Mathf.Clamp(value, 0f, cap);
        }

        void OnValidate()
        {
            initialWaveDuration = Mathf.Max(0f, initialWaveDuration);
            waveDurationIncrease = Mathf.Max(0f, waveDurationIncrease);
            maximumWaveDuration = Mathf.Max(0f, maximumWaveDuration);
            initialTimeBetweenWaves = Mathf.Max(0f, initialTimeBetweenWaves);
            timeBetweenWavesIncrease = Mathf.Max(0f, timeBetweenWavesIncrease);
            maximumTimeBetweenWaves = Mathf.Max(0f, maximumTimeBetweenWaves);
            currentWaveDuration = ClampWaveDuration(initialWaveDuration);
            currentTimeBetweenWaves = ClampTimeBetweenWaves(initialTimeBetweenWaves);
        }

        private enum WavePhase
        {
            Idle,
            Wave,
            Pause
        }
    }
}