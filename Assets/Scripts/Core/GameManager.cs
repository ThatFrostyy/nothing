using System;
using UnityEngine;
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
        [SerializeField] private bool persistAcrossScenes = true;
        [SerializeField] private bool autoAssignSpawnerOnSceneLoad = true;

        float timer;
        float currentWaveInterval;
        bool sessionActive;

        public int Wave { get; private set; } = 0;
        public int KillCount { get; private set; } = 0;

        public event Action<int> OnKillCountChanged;
        public event Action<int> OnWaveStarted;

        void Awake()
        {
            if (I != null)
            {
                Destroy(gameObject);
                return;
            }

            I = this;
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
            Debug.Log("GameManager spawned in scene: " + gameObject.scene.name);

            Application.targetFrameRate = 144;

            ResetSession(autoAssignSpawnerOnSceneLoad ? FindFirstObjectByType<EnemySpawner>() : spawner);
        }

        void OnEnable()
        {
            Enemy.OnAnyEnemyKilled += HandleEnemyKilled;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDisable()
        {
            Enemy.OnAnyEnemyKilled -= HandleEnemyKilled;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void OnDestroy()
        {
            Debug.Log("GameManager WAS DESTROYED!");
        }


        void Update()
        {
            sessionActive = spawner != null && spawner.isActiveAndEnabled;

            if (!sessionActive)
            {
                return;
            }

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
            if (!spawner || !spawner.isActiveAndEnabled)
            {
                sessionActive = false;
                return;
            }

            KillCount++;
            OnKillCountChanged?.Invoke(KillCount);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnemySpawner sceneSpawner = autoAssignSpawnerOnSceneLoad ? FindFirstObjectByType<EnemySpawner>() : spawner;
            ResetSession(sceneSpawner);
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

        void ResetSession(EnemySpawner newSpawner)
        {
            spawner = newSpawner;
            timer = 0f;
            Wave = 0;
            KillCount = 0;

            float cap = maximumTimeBetweenWaves <= 0f ? float.MaxValue : maximumTimeBetweenWaves;
            currentWaveInterval = Mathf.Clamp(initialTimeBetweenWaves, 0f, cap);

            sessionActive = spawner != null && spawner.isActiveAndEnabled;

            OnKillCountChanged?.Invoke(KillCount);
        }
    }
}
