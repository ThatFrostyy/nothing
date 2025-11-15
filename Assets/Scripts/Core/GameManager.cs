using System;
using UnityEngine;


namespace FF
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        [field: SerializeField] public int Wave { get; private set; } = 0;
        public int KillCount { get; private set; } = 0;
        [SerializeField] EnemySpawner spawner;
        [SerializeField, Min(1f)] float timeBetweenWaves = 8f;

        float timer;

        public event Action<int> OnKillCountChanged;
        public event Action<int> OnWaveStarted;

        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 120;
            KillCount = 0;
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
            if (timer >= timeBetweenWaves)
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
    }
}