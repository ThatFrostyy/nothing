using System;
using UnityEngine;
using UnityEngine.Serialization;


namespace FF
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        [field: SerializeField] public int Wave { get; private set; } = 0;
        public int KillCount { get; private set; } = 0;
        public float CurrentWaveInterval => currentWaveInterval;
        [SerializeField] EnemySpawner spawner;
        [FormerlySerializedAs("timeBetweenWaves")]
        [SerializeField, Min(0f)] float initialTimeBetweenWaves = 30f;
        [SerializeField, Min(0f)] float waveIntervalIncrease = 5f;
        [SerializeField, Min(0f)] float maximumTimeBetweenWaves = 60f;

        float timer;
        float currentWaveInterval;

        public event Action<int> OnKillCountChanged;
        public event Action<int> OnWaveStarted;

        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 120;
            KillCount = 0;
            float cap = maximumTimeBetweenWaves <= 0f ? float.MaxValue : maximumTimeBetweenWaves;
            currentWaveInterval = Mathf.Clamp(initialTimeBetweenWaves, 0f, cap);
        }

        void OnEnable()
        {
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
        }

        void OnDisable()
        {
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;
        }

        void Update()
        {
            timer += Time.deltaTime;
            float interval = Mathf.Max(0.01f, currentWaveInterval);

            if (timer >= interval)
            {
                timer = 0f;
                Wave++;
                var waveHandler = OnWaveStarted;
                if (waveHandler != null)
                {
                    waveHandler(Wave);
                }
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
            var killHandler = OnKillCountChanged;
            if (killHandler != null)
            {
                killHandler(KillCount);
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