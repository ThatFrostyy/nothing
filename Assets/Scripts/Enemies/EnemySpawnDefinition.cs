using UnityEngine;

namespace FF
{
    [CreateAssetMenu(menuName = "FF/Enemies/Spawn Definition", fileName = "EnemySpawnDefinition")]
    public class EnemySpawnDefinition : ScriptableObject
    {
        [Header("Prefab")]
        [SerializeField] private GameObject[] prefabs;

        [Header("Wave Timing")]
        [SerializeField, Min(1)] private int startWave = 1;
        [SerializeField] private int endWave = -1;
        [SerializeField, Min(1)] private int spawnInterval = 1;
        [SerializeField] private bool spawnOnlyOnBossWaves;
        [SerializeField] private bool isBoss;

        [Header("Spawn Behaviour")]
        [SerializeField] private AnimationCurve spawnCountByWave = AnimationCurve.Linear(1f, 1f, 20f, 10f);
        [SerializeField, Range(0f, 1f)] private float spawnChance = 1f;
        [SerializeField] private bool spawnInPacks = false;
        [SerializeField, Min(0.1f)] private float packRadius = 1.5f;

        [Header("Scaling Override")]
        [SerializeField] private WaveAttributeScaling attributeScalingOverride;

        [Header("Optional Audio")]
        [SerializeField] private AudioClip spawnCue;

        public GameObject[] Prefabs => prefabs;
        public int StartWave => Mathf.Max(1, startWave);
        public int EndWave => endWave;
        public int SpawnInterval => Mathf.Max(1, spawnInterval);
        public bool SpawnOnlyOnBossWaves => spawnOnlyOnBossWaves;
        public bool IsBoss => isBoss;
        public bool SpawnInPacks => spawnInPacks;
        public float PackRadius => packRadius;
        public AudioClip SpawnCue => spawnCue;
        public float SpawnChance => Mathf.Clamp01(spawnChance);

        public int EvaluateSpawnCount(int wave)
        {
            if (spawnCountByWave == null || spawnCountByWave.length == 0)
            {
                return 0;
            }

            float evaluated = spawnCountByWave.Evaluate(Mathf.Max(1, wave));
            return Mathf.Max(0, Mathf.CeilToInt(evaluated));
        }

        public EnemyWaveModifiers GetWaveModifiers(int wave, WaveAttributeScaling fallback)
        {
            WaveAttributeScaling scaling = attributeScalingOverride ?? fallback;
            return scaling != null ? scaling.CreateModifiers(wave) : EnemyWaveModifiers.Identity;
        }
    }
}
