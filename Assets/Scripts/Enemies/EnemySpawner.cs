using UnityEngine;


namespace FF
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Transform player;
        [SerializeField] float spawnRadius = 16f;
        [SerializeField] AnimationCurve countByWave = AnimationCurve.Linear(1, 6, 20, 60);


        public void SpawnWave(int wave)
        {
            if (!enemyPrefab || !player) return;

            int count = Mathf.RoundToInt(countByWave.Evaluate(wave));

            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector2 pos = (Vector2)player.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spawnRadius;
                Instantiate(enemyPrefab, pos, Quaternion.identity);
            }
        }
    }
}