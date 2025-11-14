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
                if (spawner)
                {
                    spawner.SpawnWave(Wave);
                }
            }
        }

        void HandleEnemyKilled(Enemy enemy)
        {
            KillCount++;
            OnKillCountChanged?.Invoke(KillCount);
        }
    }
}