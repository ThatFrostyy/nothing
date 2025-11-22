using System;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;


namespace FF
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public float CurrentWaveInterval => currentWaveInterval;

        [SerializeField] private EnemySpawner spawner;
        [FormerlySerializedAs("timeBetweenWaves")]
        [SerializeField, Min(0f)] private float initialTimeBetweenWaves = 30f;
        [SerializeField, Min(0f)] private float waveIntervalIncrease = 5f;
        [SerializeField, Min(0f)] private float maximumTimeBetweenWaves = 60f;

        float timer;
        float currentWaveInterval;

        public int Wave { get; private set; } = 0;
        public int KillCount { get; private set; } = 0;

        public event Action<int> OnKillCountChanged;
        public event Action<int> OnWaveStarted;

        public event Action<EnemySpawner> OnSpawnerRegistered;

        void Awake()
        {
            if (I != null)
            {
                Destroy(gameObject); 
                return;
            }

            I = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 144;

            KillCount = 0;

            float cap = maximumTimeBetweenWaves <= 0f ? float.MaxValue : maximumTimeBetweenWaves;
            currentWaveInterval = Mathf.Clamp(initialTimeBetweenWaves, 0f, cap);
        }

        void OnEnable()
        {
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearSceneRefs();
        }

        public void RegisterSpawner(EnemySpawner spawner)
        {
            this.spawner = spawner;
            OnSpawnerRegistered?.Invoke(spawner);
        }

        public void ClearSceneRefs()
        {
            spawner = null;
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