using UnityEngine;


namespace FF
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        [field: SerializeField] public int Wave { get; private set; } = 0;
        [SerializeField] EnemySpawner spawner;
        [SerializeField, Min(1f)] float timeBetweenWaves = 8f;

        float timer;

        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 120;
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= timeBetweenWaves)
            {
                timer = 0f;
                Wave++;
                if (spawner) spawner.SpawnWave(Wave);
            }
        }
    }
}